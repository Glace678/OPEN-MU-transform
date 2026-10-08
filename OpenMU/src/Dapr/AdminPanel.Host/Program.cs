// <copyright file="Program.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using MUnique.OpenMU.AdminPanel.Host;
using MUnique.OpenMU.Dapr.Common;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.ServerClients;
using MUnique.OpenMU.Persistence.EntityFramework.AdminAuth;
using MUnique.OpenMU.Web.AdminPanel;
using MUnique.OpenMU.Web.AdminPanel.API;
using MUnique.OpenMU.Web.AdminPanel.Auth;

var builder = DaprService.CreateBuilder("AdminPanel", args);

var plugInConfigurations = new List<PlugInConfiguration>();

var services = builder.Services;

services.AddPersistenceProvider(true)
    .AddPlugInManager(plugInConfigurations)
    .AddManageableServerRegistry()
    .AddSingleton<ILoginServer, LoginServer>()
    .AddSingleton<IGameServerInstanceManager, DockerGameServerInstanceManager>()
    .AddSingleton<IConnectServerInstanceManager, DockerConnectServerInstanceManager>()
    .AddAdminUserRepository();

builder.AddAdminPanel();

var metricsRegistry = new MetricsRegistry();

// todo: add some meaningful metrics
builder.AddOpenTelemetryMetrics(metricsRegistry);

var app = builder.BuildAndConfigure(false);

// Align the Dapr-hosted admin panel with the shared ConfigureAdminPanel pipeline.
// BuildAndConfigure only wires the generic Dapr middleware; without these the panel
// misses forwarded headers (Secure cookie behind nginx), rate limiting, the authorized
// /logs file endpoint and the mobile/self-registration endpoints.
app.UseForwardedHeaders();
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseRateLimiter();
app.UseAdminPanelAuth();

// The log files may contain sensitive information, so they are only served to authorized users.
var configuredLogDirectory = Environment.GetEnvironmentVariable("OPENMU_LOG_DIRECTORY");
var logDirectory = string.IsNullOrWhiteSpace(configuredLogDirectory)
    ? Path.Combine(Directory.GetCurrentDirectory(), "logs")
    : Path.GetFullPath(configuredLogDirectory, Directory.GetCurrentDirectory());
Directory.CreateDirectory(logDirectory);
app.UseAuthorizedPath("/logs");
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(logDirectory),
    RequestPath = "/logs",
});

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<MUnique.OpenMU.Web.AdminPanel.Components.App>()
    .AddInteractiveServerRenderMode();
app.MapAdminPanelAuthEndpoints();
app.MapMobileGmEndpoints();
app.MapPublicRegistrationEndpoints();

await app.WaitForDatabaseConnectionInitializationAsync().ConfigureAwait(false);

// WEB-12: migrate the admin schema during startup. A failure is logged as an error and aborts,
// instead of the lazy migration on the first request silently not completing.
await app.Services.GetRequiredService<AdminUserRepository>().InitializeStorageAsync().ConfigureAwait(false);

await app.Services.TryLoadPlugInConfigurationsAsync(plugInConfigurations).ConfigureAwait(false);

app.Run();
