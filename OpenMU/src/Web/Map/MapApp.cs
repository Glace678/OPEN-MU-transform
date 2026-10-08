// <copyright file="MapApp.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Map;

using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Web.Map.Map;

/// <summary>
/// The admin panel host class which provides a web server over ASP.NET Core Kestrel.
/// </summary>
[Obsolete]
public sealed class MapApp : IHostedService, IDisposable
{
    private const int StartPort = 4800;
    private readonly IObservableGameServer _gameServer;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MapApp> _logger;
    private IHost? _host;

    /// <summary>
    /// Initializes a new instance of the <see cref="MapApp" /> class.
    /// </summary>
    /// <param name="gameServer">The game server which contains the hosted maps.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public MapApp(IObservableGameServer gameServer, ILoggerFactory loggerFactory)
    {
        this._gameServer = gameServer;
        this._loggerFactory = loggerFactory;
        this._logger = this._loggerFactory.CreateLogger<MapApp>();
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var port = StartPort + this._gameServer.Id;

        this._logger.LogInformation($"Start initializing Map app for game server {this._gameServer.Id} on port {port}.");

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddControllers();

        builder.Services
            .AddSingleton(this._gameServer)
                .AddSingleton(this._loggerFactory)
                .AddScoped<IMapFactory, JavascriptMapFactory>();

        var app = builder.Build();

        // Unfortunately, using builder.WebHost.UseUrls(...) will be overwritten by environment variables when calling builder.Build().
        // Because we use the MapApp as an addition to another WebApplication, we want our own port.
        // Maybe, when we got some time, we could integrate the page route within the normal DaprService.
        //
        // Bind loopback by default: the live map carries real-time server/player information and has
        // no authentication of its own, so it must not listen on all network interfaces. It stays
        // reachable from the local host and from a reverse proxy on the same machine. An explicit
        // bind address can be configured with OPENMU_MAP_BIND_ADDRESS if another setup requires it.
        var bindAddress = Environment.GetEnvironmentVariable("OPENMU_MAP_BIND_ADDRESS") ?? "127.0.0.1";
        app.Configuration["urls"] = $"http://{bindAddress}:{port}";

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
        }

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapRazorComponents<MUnique.OpenMU.Web.Map.App>()
            .AddInteractiveServerRenderMode();
        app.MapControllers();

        this._host = app;
        await this._host!.StartAsync(cancellationToken).ConfigureAwait(false);
        this._logger.LogInformation($"Map app initialized for game server {this._gameServer.Id} on port {port}.");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        this._logger.LogInformation($"Stopping Map app for game server {this._gameServer.Id}");
        if (this._host is { } host)
        {
            await host.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this._host?.Dispose();
    }
}