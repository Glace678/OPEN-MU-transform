// <copyright file="WebApplicationExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel;

using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Network.Analyzer;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Web.AdminPanel.API;
using MUnique.OpenMU.Web.AdminPanel.Auth;
using MUnique.OpenMU.Web.AdminPanel.Components;
using MUnique.OpenMU.Web.AdminPanel.Services;
using MUnique.OpenMU.Web.Shared.Components.Modal;
using MUnique.OpenMU.Web.Shared.Models;
using MUnique.OpenMU.Web.Shared.Services;

/// <summary>
/// Extensions for the <see cref="WebApplicationBuilder"/>.
/// </summary>
public static class WebApplicationExtensions
{
    private const string LogDirectoryEnvironmentVariable = "OPENMU_LOG_DIRECTORY";

    /// <summary>
    /// When set (e.g. to "/api/mobile-gm"), requests that arrive on a non-loopback local interface may
    /// only target paths starting with this prefix; everything else is answered with 404. This keeps
    /// the panel UI loopback-only while exposing a single API surface to the LAN (set by the launcher).
    /// </summary>
    private const string PublicPathWhitelistEnvironmentVariable = "OPENMU_ADMIN_PUBLIC_PATH_WHITELIST";

    /// <summary>
    /// Adds the map application to the web app.
    /// When using the DaprService, call the BuildAndConfigure-Method with the parameter to add Blazor.
    /// </summary>
    /// <param name="builder">The web application builder which should be configured.</param>
    /// <param name="includeMapApp">If set to <c>true</c>, the map app is included.</param>
    /// <returns>
    /// The web application builder.
    /// </returns>
    public static WebApplicationBuilder AddAdminPanel(this WebApplicationBuilder builder, bool includeMapApp = false)
    {
        // Ensure that DataInitialization plugins will get collected - for the setup functionality.
        _ = DataInitialization.Id;

        var services = builder.Services;

        // The panel may run behind a TLS-terminating reverse proxy in a private Docker network,
        // or directly on LAN/loopback. Trust X-Forwarded-Proto from private networks so HTTPS
        // scheme detection works behind nginx/traefik, while NEVER trusting X-Forwarded-For or
        // X-Forwarded-Host unless OPENMU_TRUSTED_PROXIES explicitly lists the proxy IP/CIDR.
        services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(ConfigureAdminForwardedHeaders);

        var supportedCultures = CultureHelper
            .GetAvailableCultures<Properties.Resources>()
            .Select(culture => culture.Name)
            .ToArray();
        services.AddLocalization()
            .Configure<RequestLocalizationOptions>(o =>
            {
                o.AddSupportedCultures(supportedCultures);
                o.AddSupportedUICultures(supportedCultures);
            });
        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        if (includeMapApp)
        {
            AdminPanelEnvironment.IsHostingEmbedded = true;
        }

        services.AddControllers()
            .ConfigureApplicationPartManager(setup =>
                setup.FeatureProviders.Add(new GenericControllerFeatureProvider()));

        services.AddToasts();

        services.AddScoped<ModalService>();
        services.AddScoped<IModalService>(sp => sp.GetRequiredService<ModalService>());
        services.AddScoped<ILookupController, PersistentObjectsLookupController>();
        services.AddScoped<CreationPanelService>();

        services.AddAdminPanelAuth(builder.Configuration);
        services.AddSingleton<MobileGmService>();
        services.AddAccountSelfServiceGuard();

        services.AddSingleton<IDataSource<GameConfiguration>, GameConfigurationDataSource>();
        services.AddSingleton<IDataSource<Account>, AccountDataSource>();
        services.AddSingleton<ConfigurationSearchIndexCache>();
        services.AddSingleton<SetupService>();
        services.AddScoped<DataUpdateService>();
        services.AddScoped<AccountService>();
        services.AddScoped<IDataService<Account>>(serviceProvider => serviceProvider.GetService<AccountService>()!);
        services.AddScoped<PlugInController>();
        services.AddScoped<IDataService<PlugInConfigurationViewItem>>(serviceProvider => serviceProvider.GetService<PlugInController>()!);
        services.AddScoped<ChatCommandController>();
        services.AddScoped<IDataService<ChatCommandViewItem>>(serviceProvider => serviceProvider.GetService<ChatCommandController>()!);
        services.AddScoped<AdminUserManagementService>();
        services.AddScoped<IChangeNotificationService, ChangeNotificationService>();

        // The analyzers are only created when the network analyzer page is actually used,
        // which requires an IPacketCaptureService - that's only registered in the all-in-one
        // deployment.
        services.AddSingleton<PacketAnalyzerProvider>();
        services.AddScoped<NavigationHistory>();
        services.AddScoped<LoggedInAccountService>();
        services.AddScoped<LoadingOverlayService>();
        services.AddScoped<IDataService<LoggedInAccount>>(serviceProvider => serviceProvider.GetService<LoggedInAccountService>()!);
        services.AddScoped<OfflineAccountService>();
        services.AddScoped<IDataService<OfflineAccount>>(serviceProvider => serviceProvider.GetService<OfflineAccountService>()!);

        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
        return builder;
    }

    /// <summary>
    /// Configures the admin panel.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The configured web application.</returns>
    public static WebApplication ConfigureAdminPanel(this WebApplication app)
    {
        // Must run before any other middleware so scheme/host/client IP from the
        // reverse proxy are visible to redirects, auth cookie policy, etc.
        app.UseForwardedHeaders();
        app.UsePublicPathWhitelist();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
        }

        app.UseStaticFiles();

        app.UseRequestLocalization();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAdminPanelAuth();

        // The log files may contain sensitive information, so they are only served to authorized users.
        app.UseAuthorizedPath("/logs");
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = CreateLogFileProvider(),
            RequestPath = "/logs",
        });

        app.UseAntiforgery();

        app.MapStaticAssets();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.MapControllers().RequireAuthorization();
        app.MapAdminPanelAuthEndpoints();
        app.MapMobileGmEndpoints();
        app.MapPublicRegistrationEndpoints();

        AdminPanelEnvironment.IsHostingEmbedded = true;

        return app;
    }

    /// <summary>Configures safe forwarded-header processing for AdminPanel hosts.</summary>
    /// <param name="options">The forwarded headers options.</param>
    internal static void ConfigureAdminForwardedHeaders(Microsoft.AspNetCore.Builder.ForwardedHeadersOptions options)
    {
        options.ForwardLimit = 1;
        var trustedProxies = Environment.GetEnvironmentVariable("OPENMU_TRUSTED_PROXIES");
        if (!string.IsNullOrWhiteSpace(trustedProxies))
        {
            options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();
            foreach (var entry in trustedProxies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var slashIndex = entry.IndexOf('/');
                if (slashIndex > 0
                    && System.Net.IPAddress.TryParse(entry.AsSpan(0, slashIndex), out var networkAddress)
                    && int.TryParse(entry.AsSpan(slashIndex + 1), out var prefixLength))
                {
                    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(networkAddress, prefixLength));
                }
                else if (System.Net.IPAddress.TryParse(entry, out var proxyIp))
                {
                    options.KnownProxies.Add(proxyIp);
                }
            }

            return;
        }

        options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("10.0.0.0"), 8));
        options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("172.16.0.0"), 12));
        options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("192.168.0.0"), 16));
    }

    /// <summary>
    /// Restricts non-loopback requests to the configured path segment prefix when
    /// <c>OPENMU_ADMIN_PUBLIC_PATH_WHITELIST</c> is set.
    /// </summary>
    /// <param name="app">The application builder.</param>
    internal static void UsePublicPathWhitelist(this IApplicationBuilder app)
    {
        var publicPathWhitelist = Environment.GetEnvironmentVariable(PublicPathWhitelistEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(publicPathWhitelist))
        {
            return;
        }

        var trimmed = publicPathWhitelist.Trim();
        var allowedPrefix = new Microsoft.AspNetCore.Http.PathString(trimmed.StartsWith('/') ? trimmed : "/" + trimmed);
        app.Use(async (context, next) =>
        {
            var localAddress = NormalizeAddress(context.Connection.LocalIpAddress);
            var remoteAddress = NormalizeAddress(context.Connection.RemoteIpAddress);
            var isDirectLoopback = localAddress is not null
                && remoteAddress is not null
                && System.Net.IPAddress.IsLoopback(localAddress)
                && System.Net.IPAddress.IsLoopback(remoteAddress);

            if (!isDirectLoopback
                && !context.Request.Path.StartsWithSegments(allowedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status404NotFound;
                return;
            }

            await next().ConfigureAwait(false);
        });
    }

    private static System.Net.IPAddress? NormalizeAddress(System.Net.IPAddress? address) =>
        address is { IsIPv4MappedToIPv6: true } ? address.MapToIPv4() : address;

    /// <summary>Creates a file provider for the configured log directory, creating it when needed.</summary>
    /// <returns>The log directory file provider.</returns>
    internal static PhysicalFileProvider CreateLogFileProvider()
    {
        var configuredDirectory = Environment.GetEnvironmentVariable(LogDirectoryEnvironmentVariable);
        var logDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(Directory.GetCurrentDirectory(), "logs")
            : Path.GetFullPath(configuredDirectory, Directory.GetCurrentDirectory());
        Directory.CreateDirectory(logDirectory);
        return new PhysicalFileProvider(logDirectory);
    }
}
