// <copyright file="Program.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Startup;

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.ChatServer;
using MUnique.OpenMU.ConnectServer;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.FriendServer;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GuildServer;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.LoginServer;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Analyzer;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AdminAuth;
using MUnique.OpenMU.Persistence.EntityFramework.Json;
using MUnique.OpenMU.Persistence.Initialization;
using MUnique.OpenMU.Persistence.Initialization.Version075;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Web.AdminPanel;
using MUnique.OpenMU.Web.AdminPanel.API;
using MUnique.OpenMU.Web.AdminPanel.Services;
using MUnique.OpenMU.Web.Map.Map;
using MUnique.OpenMU.Web.Shared;
using Nito.AsyncEx.Synchronous;
using Serilog;
using Serilog.Debugging;

/// <summary>
/// The startup class for an all-in-one game server.
/// </summary>
internal sealed class Program : IDisposable
{
    private static bool _confirmExit;
    private static SystemConfiguration? _systemConfiguration;

    /// <summary>
    /// Gets the resolved server time zone (from <see cref="SystemConfiguration.TimeZoneId"/>),
    /// used to interpret the times of periodic event schedules. Defaults to <see cref="TimeZoneInfo.Utc"/>.
    /// </summary>
    internal static TimeZoneInfo ServerTimeZone { get; private set; } = TimeZoneInfo.Utc;

    private readonly IDictionary<int, IGameServer> _gameServers = new Dictionary<int, IGameServer>();
    private readonly IList<IManageableServer> _servers = new List<IManageableServer>();
    private readonly Serilog.ILogger _logger;
    private readonly IConfiguration _configuration;

    private IHost? _serverHost;

    /// <summary>
    /// Initializes a new instance of the <see cref="Program"/> class.
    /// </summary>
    public Program()
    {
        AppDomain.CurrentDomain.UnhandledException += this.OnUnhandledException;
        SelfLog.Enable(Console.Error);
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false, true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true, true)
            .AddEnvironmentVariables()
            .Build();

        this._configuration = configuration;
        this._logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();
    }

    /// <summary>
    /// The main method.
    /// </summary>
    /// <param name="args">The command line args.</param>
    public static async Task Main(string[] args)
    {
        using var exitCts = new CancellationTokenSource();
        var exitToken = exitCts.Token;

        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            if (_confirmExit)
            {
#pragma warning disable VSTHRD103 // Call async methods when in an async method
                exitCts.Cancel();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
                Console.CancelKeyPress -= OnCancelKeyPress;
                Console.WriteLine("\nBye! Press enter to finish");
            }
            else
            {
                _confirmExit = true;
                Console.Write("\nConfirm shutdown? (y/N) ");
            }
        }

        void OnProcessExit(object? sender, EventArgs eventArgs)
        {
            if (!exitToken.IsCancellationRequested)
            {
#pragma warning disable VSTHRD103 // Call async methods when in an async method
                exitCts.Cancel();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
                Debug.WriteLine("KILL");
            }
        }

        Console.CancelKeyPress += OnCancelKeyPress;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        try
        {
            using var program = new Program();
            await program.InitializeAsync(args).ConfigureAwait(false);
            while (!exitToken.IsCancellationRequested && !program.IsStopping)
            {
                await Task.Delay(100).ConfigureAwait(false);

                if (_systemConfiguration?.ReadConsoleInput is true)
                {
                    await HandleConsoleInputAsync(exitCts, exitToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Program"/> class.
    /// </summary>
    /// <param name="args">The command line args.</param>
    public async Task InitializeAsync(string[] args)
    {
        JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());

        this._logger.Information("Creating host...");
        this._serverHost = await this.CreateHostAsync(args).ConfigureAwait(false);

        var autoStart = _systemConfiguration?.AutoStart is true
                        || args.Contains("-autostart")
                        || !this.IsAdminPanelEnabled(args);

        if (_systemConfiguration is { }
            && this._serverHost.Services.GetService<IIpAddressResolver>() is ConfigurableIpResolver resolver)
        {
            resolver.Configure(_systemConfiguration.IpResolver, _systemConfiguration.IpResolverParameter);
        }

        if (autoStart)
        {
            foreach (var chatServer in this._servers.OfType<ChatServer>())
            {
                await chatServer.StartAsync().ConfigureAwait(false);
            }

            foreach (var gameServer in this._gameServers.Values)
            {
                await gameServer.StartAsync().ConfigureAwait(false);
            }

            foreach (var connectServer in this._servers.OfType<IConnectServer>())
            {
                await connectServer.StartAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this._serverHost?.StopAsync().WaitAndUnwrapException();
        this._serverHost?.Dispose();
    }

    private bool IsStopping => this._serverHost?.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested is true;

    private static void DisplayCommands()
    {
        var commandList = "help, exit, gc, pid";
        Console.WriteLine($"Commands available: {commandList}");
    }

    private static async Task HandleConsoleInputAsync(CancellationTokenSource exitCts, CancellationToken exitToken)
    {
        Console.Write("> ");
        var input = (await Console.In.ReadLineAsync(exitToken).ConfigureAwait(false))?.ToLower();

        switch (input)
        {
            case "y" when _confirmExit:
            case "exit":
                await exitCts.CancelAsync().ConfigureAwait(false);
                break;
            case "gc":
                GC.Collect();
                Console.WriteLine("Garbage Collected!");
                break;
            case "pid":
                var process = Process.GetCurrentProcess();
                var pid = process.Id.ToString();
                Console.WriteLine($"PID: {pid}");
                break;
            case "?":
            case "help":
                DisplayCommands();
                break;
            case "":
            case null:
                break;
            default:
                Console.WriteLine("Unknown command");
                DisplayCommands();
                break;
        }

        if (_confirmExit && !string.IsNullOrWhiteSpace(input))
        {
            _confirmExit = false;
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.IsTerminating)
        {
            this._logger.Fatal(e.ExceptionObject as Exception, "Unhandled exception leading to terminating application: {0}", e.ExceptionObject);
        }
        else
        {
            this._logger.Error(e.ExceptionObject as Exception, "Unhandled exception: {0}", e.ExceptionObject);
        }
    }

    private async Task<IHost> CreateHostAsync(string[] args)
    {
        // Ensure GameLogic and GameServer Assemblies are loaded
        _ = GameLogic.Rand.NextInt(1, 2);
        _ = DataInitialization.Id;
        _ = OpenMU.GameServer.ClientVersionResolver.DefaultVersion;

        var addAdminPanel = this.IsAdminPanelEnabled(args);
        await new ConfigFileDatabaseConnectionStringProvider().InitializeAsync(default).ConfigureAwait(false);

        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog(this._logger);
        if (addAdminPanel)
        {
            // The storage of the admin panel users has to be registered before the panel itself,
            // which only adds a fallback when nothing else is registered.
            builder.Services.AddAdminUserRepository();
            builder.AddAdminPanel(includeMapApp: true);
        }

        builder.Services.AddSingleton(this._servers)
            .AddSingleton<IConfigurationChangePublisher, ConfigurationChangeHandler>()
            .AddSingleton<IConfigurationChangeListener, ConfigurationChangeListener>()
            .AddSingleton<ConfigurationChangeMediator>()
            .AddSingleton<IConfigurationChangeMediator>(s => s.GetRequiredService<ConfigurationChangeMediator>())
            .AddSingleton<IConfigurationChangeMediatorListener>(s => s.GetRequiredService<ConfigurationChangeMediator>())
            .AddSingleton(s => this.CreateIpResolver(s, args))
            .AddSingleton(this._gameServers)
            .AddSingleton(this._gameServers.Values)
            .AddSingleton(s =>
                this.DeterminePersistenceContextProviderAsync(
                    args,
                    s.GetService<ILoggerFactory>() ?? throw new InvalidOperationException($"{nameof(ILoggerFactory)} not registered."),
                    s.GetService<IConfigurationChangeListener>() ?? throw new InvalidOperationException($"{nameof(IConfigurationChangeListener)} not registered."),
                    s.GetService<IConfigurationChangePublisher>() ?? throw new InvalidOperationException($"{nameof(IConfigurationChangePublisher)} not registered."))
                    .WaitAndUnwrapException())
            .AddSingleton<IPersistenceContextProvider>(s => s.GetService<IMigratableDatabaseContextProvider>()!)
            .AddSingleton<Lazy<IPersistenceContextProvider>>(s => new(() => s.GetService<IMigratableDatabaseContextProvider>()!))
            .AddSingleton<ILoginServer, LoginServer>()
            .AddSingleton<IGuildServer, GuildServer>()
            .AddSingleton<IFriendServer, FriendServer>()
            .AddSingleton<ChatServer>()
            .AddSingleton<IChatServer>(s => s.GetService<ChatServer>()!)
            .AddSingleton<ConnectServerFactory>()
            .AddSingleton<ConnectServerContainer>()
            .AddSingleton<IConnectServerInstanceManager>(provider => provider.GetService<ConnectServerContainer>()!)
            .AddSingleton<GameServerContainer>()
            .AddSingleton<IGameServerInstanceManager>(provider => provider.GetService<GameServerContainer>()!)
            .AddScoped<IMapFactory, JavascriptMapFactory>()
            .AddSingleton<SetupService>()
            .AddSingleton<IEnumerable<IConnectServer>>(provider => provider.GetService<ConnectServerContainer>() ?? throw new Exception($"{nameof(ConnectServerContainer)} not registered."))
            .AddSingleton<IGuildChangePublisher, GuildChangeToGameServerPublisher>()
            .AddSingleton<IFriendNotifier, FriendNotifierToGameServer>()
            .AddSingleton<PlugInManager>()
            .AddSingleton<IServerProvider, LocalServerProvider>()
            .AddSingleton<IPacketCaptureService, PacketCaptureService>()
            .AddHostedService<LocalControlService>()
            .AddSingleton<ICollection<PlugInConfiguration>>(this.PlugInConfigurationsFactory)
            .AddTransient<ReferenceHandler, ByDataSourceReferenceHandler>(provider =>
            {
                var persistenceContextProvider = provider.GetService<IPersistenceContextProvider>();
                var dataSource = new GameConfigurationDataSource(
                    provider.GetService<ILogger<GameConfigurationDataSource>>()!,
                    persistenceContextProvider!);
                var configId = persistenceContextProvider!.CreateNewConfigurationContext().GetDefaultGameConfigurationIdAsync(default).AsTask().WaitAndUnwrapException();
                dataSource.GetOwnerAsync(configId!.Value).AsTask().WaitAndUnwrapException();
                var referenceHandler = new ByDataSourceReferenceHandler(dataSource);
                return referenceHandler;
            })
            .AddSingleton<IDataSource<GameConfiguration>, GameConfigurationDataSource>()
            .AddHostedService<ChatServerContainer>()
            .AddHostedService<GameServerContainer>()
            .AddHostedService(provider => provider.GetService<GameServerContainer>()!)
            .AddHostedService(provider => provider.GetService<ConnectServerContainer>()!)
            .AddControllers().AddApplicationPart(typeof(ServerController).Assembly);

        var host = builder.Build();

        // NpgsqlLoggingConfiguration.InitializeLogging(host.Services.GetRequiredService<ILoggerFactory>())
        this._logger.Information("Host created");

        if (addAdminPanel)
        {
            host.ConfigureAdminPanel();
        }

        this._logger.Information("Starting host...");
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        await host.StartAsync().ConfigureAwait(false);
        stopwatch.Stop();
        this._logger.Information("Host started, elapsed time: {elapsed}", stopwatch.Elapsed);
        this._logger.Information("Admin Panel bound to urls: {urls}", string.Join("; ", host.Urls));
        return host;
    }

    private IIpAddressResolver CreateIpResolver(IServiceProvider serviceProvider, string[] args)
    {
        (IpResolverType IpResolver, string? IpResolverParameter)? settings = default;
        if (_systemConfiguration is not null)
        {
            settings = (_systemConfiguration.IpResolver, _systemConfiguration.IpResolverParameter);
        }

        return IpAddressResolverFactory.CreateIpResolver(args, settings, serviceProvider.GetService<ILoggerFactory>()!);
    }

    private ICollection<PlugInConfiguration> PlugInConfigurationsFactory(IServiceProvider serviceProvider)
    {
        var persistenceContextProvider = serviceProvider.GetService<IPersistenceContextProvider>() ?? throw new Exception($"{nameof(IPersistenceContextProvider)} not registered.");
        using var context = persistenceContextProvider.CreateNewTypedContext(typeof(PlugInConfiguration), false);

        var configs = context.GetAsync<PlugInConfiguration>().AsTask().WaitAndUnwrapException().ToList();

        var referenceHandler = new ByDataSourceReferenceHandler(
            new GameConfigurationDataSource(serviceProvider.GetService<ILogger<GameConfigurationDataSource>>()!, persistenceContextProvider));

        // We check if we miss any plugin configurations in the database. If we do, we try to add them.
        var pluginManager = new PlugInManager(null, serviceProvider.GetService<ILoggerFactory>()!, serviceProvider, referenceHandler);
        pluginManager.DiscoverAndRegisterPlugIns();

        var typesWithCustomConfig = pluginManager.KnownPlugInTypes.Where(t => t.GetInterfaces().Contains(typeof(ISupportDefaultCustomConfiguration))).ToDictionary(t => t.GUID, t => t);

        using var notificationSuspension = context.SuspendChangeNotifications();
        var typesWithMissingCustomConfigs = configs.Where(c => string.IsNullOrWhiteSpace(c.CustomConfiguration) && typesWithCustomConfig.ContainsKey(c.TypeId)).ToList();
        if (typesWithMissingCustomConfigs.Any())
        {
            typesWithMissingCustomConfigs.ForEach(c => this.CreateDefaultPlugInConfiguration(typesWithCustomConfig[c.TypeId]!, c, referenceHandler));
            _ = context.SaveChangesAsync().AsTask().WaitAndUnwrapException();
        }

        var typesWithMissingConfigs = pluginManager.KnownPlugInTypes.Where(t => configs.All(c => c.TypeId != t.GUID)).ToList();
        if (!typesWithMissingConfigs.Any())
        {
            return configs;
        }

        configs.AddRange(this.CreateMissingPlugInConfigurations(typesWithMissingConfigs, persistenceContextProvider, referenceHandler));
        _ = context.SaveChangesAsync().AsTask().WaitAndUnwrapException();
        return configs;
    }

    private IEnumerable<PlugInConfiguration> CreateMissingPlugInConfigurations(IEnumerable<Type> plugInTypes, IPersistenceContextProvider persistenceContextProvider, ReferenceHandler referenceHandler)
    {
        GameConfiguration gameConfiguration;

        using (var context = persistenceContextProvider.CreateNewContext())
        {
            gameConfiguration = context.GetAsync<GameConfiguration>().AsTask().WaitAndUnwrapException().First();
        }

        using var saveContext = persistenceContextProvider.CreateNewContext(gameConfiguration);
        saveContext.Attach(gameConfiguration);
        foreach (var plugInType in plugInTypes)
        {
            var plugInConfiguration = saveContext.CreateNew<PlugInConfiguration>();
            plugInConfiguration.TypeId = plugInType.GUID;
            plugInConfiguration.IsActive = !plugInType.IsAssignableTo(typeof(IDisabledByDefault));
            gameConfiguration.PlugInConfigurations.Add(plugInConfiguration);
            if (plugInType.GetInterfaces().Contains(typeof(ISupportDefaultCustomConfiguration)))
            {
                this.CreateDefaultPlugInConfiguration(plugInType, plugInConfiguration, referenceHandler);
            }

            yield return plugInConfiguration;
        }

        using var notificationSuspension = saveContext.SuspendChangeNotifications();
        _ = saveContext.SaveChangesAsync().AsTask().WaitAndUnwrapException();
    }

    private void CreateDefaultPlugInConfiguration(Type plugInType, PlugInConfiguration plugInConfiguration, ReferenceHandler referenceHandler)
    {
        try
        {
            var plugin = (ISupportDefaultCustomConfiguration)Activator.CreateInstance(plugInType)!;
            var defaultConfig = plugin.CreateDefaultConfig();
            plugInConfiguration.SetConfiguration(defaultConfig, referenceHandler);
        }
        catch (Exception ex)
        {
            this._logger.Warning(ex, "Could not create custom default configuration for plugin type {plugInType}", plugInType);
        }
    }

    private ushort DetermineUshort(string parameterName, string[] args, ushort defaultValue)
    {
        var parameter = args.FirstOrDefault(a => a.StartsWith($"-{parameterName}:", StringComparison.InvariantCultureIgnoreCase));
        if (parameter is null)
        {
            return defaultValue;
        }

        return ushort.TryParse(parameter.Substring(parameter.IndexOf(':') + 1), out var value)
            ? value
            : throw new ArgumentException($"Parameter -{parameterName} must be an unsigned 16-bit integer.", nameof(args));
    }

    private bool IsAdminPanelEnabled(string[] args) => this.IsFeatureEnabled("adminpanel", args);

    private bool IsFeatureEnabled(string featureName, string[] args)
    {
        var parameter = args.FirstOrDefault(a => a.StartsWith($"-{featureName}:", StringComparison.InvariantCultureIgnoreCase));
        if (parameter is null)
        {
            return true;
        }

        return parameter.Substring(parameter.IndexOf(':') + 1).StartsWith("enabled", StringComparison.InvariantCultureIgnoreCase);
    }

    private string GetVersionParameter(string[] args)
    {
        var parameter = args.FirstOrDefault(a => a.StartsWith("-version:", StringComparison.InvariantCultureIgnoreCase));
        if (parameter is null)
        {
            return "season6"; // default
        }

        return parameter.Substring(parameter.IndexOf(':') + 1);
    }

    private async Task<IMigratableDatabaseContextProvider> DeterminePersistenceContextProviderAsync(string[] args, ILoggerFactory loggerFactory, IConfigurationChangeListener changeListener, IConfigurationChangePublisher changePublisher)
    {
        var version = this.GetVersionParameter(args);
        var balanceV1Profile = BalanceV1.GetRequestedProfile(args);
        var soloRequested = args.Any(argument => argument.Equals("-solo", StringComparison.OrdinalIgnoreCase));
        if (soloRequested && balanceV1Profile is not null)
        {
            throw new ArgumentException("The -solo and -balance-v1 profiles are mutually exclusive.", nameof(args));
        }

        IMigratableDatabaseContextProvider contextProvider;
        if (args.Contains("-demo"))
        {
            var inMemoryProvider = new InMemoryPersistenceContextProvider();
            contextProvider = inMemoryProvider;
            await this.InitializeDataAsync(version, loggerFactory, contextProvider, args).ConfigureAwait(false);
            inMemoryProvider.ChangePublisher = changePublisher;
        }
        else
        {
            contextProvider = await this.PrepareRepositoryProviderAsync(args.Contains("-reinit"), version, args, loggerFactory, changeListener).ConfigureAwait(false);
        }

        if (soloRequested)
        {
            if (!string.Equals(version, "season6", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The solo profile requires -version:season6.");
            }

            await this.ApplySoloBalanceAsync(contextProvider).ConfigureAwait(false);
        }
        else if (balanceV1Profile is { } profile)
        {
            if (!string.Equals(version, "season6", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The balance-v1 profile requires -version:season6.", nameof(args));
            }

            var mayInstall = args.Any(argument => argument.Equals("-demo", StringComparison.OrdinalIgnoreCase)
                                                  || argument.Equals("-reinit", StringComparison.OrdinalIgnoreCase));
            await this.ApplyBalanceV1Async(contextProvider, profile, mayInstall).ConfigureAwait(false);
        }

        if (soloRequested || balanceV1Profile is not null)
        {
            await InitializeLocalGameAccountAsync(contextProvider).ConfigureAwait(false);
        }

        await ValidateInstalledBalanceProfilesAsync(contextProvider).ConfigureAwait(false);

        await this.ReadSystemConfigurationAsync(contextProvider).ConfigureAwait(false);

        return contextProvider;
    }

    private static async Task InitializeLocalGameAccountAsync(IPersistenceContextProvider provider)
    {
        var username = Environment.GetEnvironmentVariable("OPENMU_LOCAL_GAME_USERNAME");
        var password = Environment.GetEnvironmentVariable("OPENMU_LOCAL_GAME_PASSWORD");
        if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
        {
            return;
        }

        var bindAddress = Environment.GetEnvironmentVariable("OPENMU_BIND_ADDRESS");
        var controlPipe = Environment.GetEnvironmentVariable("OPENMU_CONTROL_PIPE");
        var mobilePackageKey = Environment.GetEnvironmentVariable("OPENMU_MOBILE_PACKAGE_KEY");
        var mobileMode = !string.IsNullOrEmpty(mobilePackageKey);
        var validEnvironment = mobileMode
            ? IsValidMobilePackageKey(mobilePackageKey) && IsWildcardAddress(bindAddress)
            : string.Equals(bindAddress, "127.0.0.1", StringComparison.Ordinal);
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)
            || string.IsNullOrWhiteSpace(controlPipe)
            || !validEnvironment)
        {
            throw new InvalidOperationException(
                "Automatic game account setup requires the local launcher security boundary; "
                + "mobile mode additionally requires a valid package key and wildcard binding.");
        }

        using var readContext = provider.CreateNewConfigurationContext();
        var configuration = (await readContext.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        using var context = provider.CreateNewPlayerContext(configuration);
        await SoloAccountInitializer.EnsureAsync(context, username, password).ConfigureAwait(false);
    }

    private static bool IsValidMobilePackageKey(string? packageKey) =>
        packageKey is { Length: 43 }
        && packageKey.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static bool IsWildcardAddress(string? address) =>
        IPAddress.TryParse(address, out var parsedAddress)
        && (parsedAddress.Equals(IPAddress.Any) || parsedAddress.Equals(IPAddress.IPv6Any));

    private async Task ApplySoloBalanceAsync(IPersistenceContextProvider provider)
    {
        using var readContext = provider.CreateNewConfigurationContext();
        foreach (var configuration in await readContext.GetAsync<GameConfiguration>().ConfigureAwait(false))
        {
            BalanceV1.ValidateProfileMarkers(configuration);
            if (BalanceV1.IsEnabled(configuration))
            {
                throw new InvalidOperationException(
                    "Cannot install the Solo profile because this configuration already uses balance-v1.");
            }

            if (SoloBalance.IsEnabled(configuration))
            {
                continue;
            }

            using var context = provider.CreateNewContext(configuration);
            using var suspension = context.SuspendChangeNotifications();
            context.Attach(configuration);
            new SoloBalanceInitializer(context, configuration).Initialize();
            await context.SaveChangesAsync().ConfigureAwait(false);
            this._logger.Information("Solo balance v1 installed; existing character progress was preserved.");
        }
    }

    private async Task ApplyBalanceV1Async(
        IPersistenceContextProvider provider,
        BalanceV1.ExperienceProfile profile,
        bool mayInstall)
    {
        using var readContext = provider.CreateNewConfigurationContext();
        var configurations = await readContext.GetAsync<GameConfiguration>().ConfigureAwait(false);
        foreach (var configuration in configurations)
        {
            BalanceV1.ValidateProfileMarkers(configuration);
            if (SoloBalance.IsEnabled(configuration))
            {
                throw new InvalidOperationException(
                    "Cannot install balance-v1 because this configuration already uses the mutually exclusive Solo profile.");
            }

            if (BalanceV1.IsEnabled(configuration))
            {
                _ = BalanceV1Initializer.ValidateCoreConfiguration(configuration, profile);
            }
            else if (!mayInstall)
            {
                throw new InvalidOperationException(
                    "Installing balance-v1 into an existing configuration is blocked. "
                    + "Create an isolated configuration explicitly with -reinit or use -demo.");
            }
        }

        foreach (var configuration in configurations.Where(configuration => !BalanceV1.IsEnabled(configuration)))
        {
            using var context = provider.CreateNewContext(configuration);
            using var suspension = context.SuspendChangeNotifications();
            context.Attach(configuration);
            new BalanceV1Initializer(context, configuration, profile).Initialize();
            await context.SaveChangesAsync().ConfigureAwait(false);
            this._logger.Information(
                "balance-v1 installed with the {Profile} experience profile; existing character data was not migrated.",
                BalanceV1.GetProfileId(profile));
        }
    }

    private static async Task ValidateInstalledBalanceProfilesAsync(IPersistenceContextProvider provider)
    {
        using var readContext = provider.CreateNewConfigurationContext();
        foreach (var configuration in await readContext.GetAsync<GameConfiguration>().ConfigureAwait(false))
        {
            BalanceV1.ValidateProfileMarkers(configuration);
            if (BalanceV1.IsEnabled(configuration))
            {
                _ = BalanceV1Initializer.ValidateCoreConfiguration(configuration);
            }
        }
    }

    private async Task<IMigratableDatabaseContextProvider> PrepareRepositoryProviderAsync(bool reinit, string version, string[] args, ILoggerFactory loggerFactory, IConfigurationChangeListener changeListener)
    {
        var contextProvider = new PersistenceContextProvider(loggerFactory, changeListener);
        if (reinit || !await contextProvider.DatabaseExistsAsync().ConfigureAwait(false))
        {
            // The database can be provisioned externally (e.g. by a Kubernetes operator,
            // infrastructure-as-code, or a managed cloud database), where the connecting role is
            // typically not permitted to create or drop databases. Enabling
            // Database:AssumeExternallyProvisioned keeps the existing database and only builds its
            // schema via migrations, instead of dropping and recreating it. The default (false)
            // preserves the original drop-and-recreate behaviour; an explicit -reinit always recreates.
            var assumeExternallyProvisioned = !reinit
                && this._configuration.GetValue<bool>("Database:AssumeExternallyProvisioned");

            this._logger.Information(assumeExternallyProvisioned
                ? "Building the schema on the externally-provisioned database (no drop/create)..."
                : "The database is getting (re-)initialized...");
            using var update = await contextProvider.ReCreateDatabaseAsync(dropExistingDatabase: !assumeExternallyProvisioned).ConfigureAwait(false);
            await this.InitializeDataAsync(version, loggerFactory, contextProvider, args).ConfigureAwait(false);
            this._logger.Information("...initialization finished.");
        }
        else if (!await contextProvider.IsDatabaseUpToDateAsync().ConfigureAwait(false))
        {
            if (_systemConfiguration?.AutoUpdateSchema is true || await contextProvider.ShouldDoAutoSchemaUpdateAsync().ConfigureAwait(false))
            {
                Console.WriteLine("The database schema needs to be updated before the server can be started. Updating...");
                await contextProvider.ApplyAllPendingUpdatesAsync().ConfigureAwait(false);
                Console.WriteLine("The database schema has been successfully updated.");
            }
            else
            {
                Console.WriteLine("The database schema needs to be updated before the server can be started. Apply update? (y/n)");
                var key = Console.ReadLine()?.ToLowerInvariant();
                if (key == "y")
                {
                    await contextProvider.ApplyAllPendingUpdatesAsync().ConfigureAwait(false);
                    Console.WriteLine("The database schema has been successfully updated.");
                }
                else
                {
                    Console.WriteLine("Cancelled the schema update process, can't start the server.");
                    return null!;
                }
            }
        }
        else
        {
            // everything is fine and ready
        }

        return contextProvider;
    }

    private async Task ReadSystemConfigurationAsync(IPersistenceContextProvider persistenceContextProvider)
    {
        using var context = persistenceContextProvider.CreateNewTypedContext(typeof(SystemConfiguration), false);
        var config = (await context.GetAsync<SystemConfiguration>().ConfigureAwait(false)).FirstOrDefault();
        if (config != null)
        {
            _systemConfiguration = config;
        }

        ServerTimeZone = this.ResolveServerTimeZone(_systemConfiguration?.TimeZoneId);
    }

    private TimeZoneInfo ResolveServerTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            // FindSystemTimeZoneById accepts both IANA and Windows ids on all platforms since .NET 6 (ICU).
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            this._logger.Warning(ex, "Could not resolve configured server time zone '{TimeZoneId}'. Falling back to UTC.", timeZoneId);
            return TimeZoneInfo.Utc;
        }
    }

    private async Task InitializeDataAsync(string version, ILoggerFactory loggerFactory, IPersistenceContextProvider contextProvider, string[]? args = null)
    {
        var serviceContainer = new ServiceContainer();
        serviceContainer.AddService(typeof(ILoggerFactory), loggerFactory);
        serviceContainer.AddService(typeof(IPersistenceContextProvider), contextProvider);

        var referenceHandler = new ByDataSourceReferenceHandler(
            new GameConfigurationDataSource(serviceContainer.GetService<ILogger<GameConfigurationDataSource>>()!, contextProvider));

        var plugInManager = new PlugInManager(null, loggerFactory, serviceContainer, referenceHandler);
        plugInManager.DiscoverAndRegisterPlugInsOf<IDataInitializationPlugIn>();
        var initialization = plugInManager.GetStrategy<IDataInitializationPlugIn>(version) ?? throw new Exception("Data initialization plugin not found");
        var gameServerCount = this.DetermineUshort("gameservers", args ?? Array.Empty<string>(), 3);
        if (gameServerCount is 0 or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(args), gameServerCount, "The game server count must be between 1 and 255.");
        }

        var createTestAccounts = this.DetermineBoolean("testaccounts", args ?? Array.Empty<string>(), true);
        await initialization.CreateInitialDataAsync((byte)gameServerCount, createTestAccounts).ConfigureAwait(false);
    }

    private bool DetermineBoolean(string parameterName, string[] args, bool defaultValue)
    {
        var parameter = args.FirstOrDefault(a => a.StartsWith($"-{parameterName}:", StringComparison.InvariantCultureIgnoreCase));
        return parameter is null
            ? defaultValue
            : bool.TryParse(parameter.Substring(parameter.IndexOf(':') + 1), out var value)
                ? value
                : throw new ArgumentException($"Parameter -{parameterName} must be 'true' or 'false'.", nameof(args));
    }
}
