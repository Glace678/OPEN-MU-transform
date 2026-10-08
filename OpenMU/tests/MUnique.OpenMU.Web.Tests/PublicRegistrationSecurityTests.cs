// <copyright file="PublicRegistrationSecurityTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests;

using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using Bunit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.API;
using MUnique.OpenMU.Web.AdminPanel.Pages;
using MUnique.OpenMU.Web.AdminPanel.Properties;

/// <summary>Exercises the mapped reset boundary without starting a server or accessing a database.</summary>
[TestFixture]
public class PublicRegistrationSecurityTests
{
    private const string SettingName = "AccountSelfService:AllowLocalPasswordReset";
    private const string TokenFileSettingName = "AccountSelfService:MaintenanceTokenFile";
    private static readonly PasswordResetRequest ValidRequest = new("solotest", "new-test-password", "new-test-password");

    /// <summary>Missing, false and invalid configuration fail closed, whatever the peer is.</summary>
    [TestCase(null)]
    [TestCase("false")]
    [TestCase("")]
    [TestCase("1")]
    [TestCase("yes")]
    public async Task DefaultOrInvalidConfigurationRejectsWithoutStorageAsync(string? setting)
    {
        var persistence = new Mock<IPersistenceContextProvider>(MockBehavior.Strict);

        var response = await InvokeResetAsync(persistence.Object, setting, "127.0.0.1").ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(response.Success, Is.False);
            Assert.That(response.Code, Is.EqualTo("reset_disabled"));
            Assert.That(response.Message, Does.Contain("administrator"));
        });
        persistence.VerifyNoOtherCalls();
    }

    /// <summary>Enabling maintenance never trusts the network position: without the token every peer is rejected.</summary>
    [TestCase(null)]
    [TestCase("0.0.0.0")]
    [TestCase("::")]
    [TestCase("192.168.50.4")]
    [TestCase("203.0.113.20")]
    [TestCase("2001:db8::1")]
    [TestCase("::ffff:203.0.113.20")]
    [TestCase("127.0.0.1")]
    [TestCase("::1")]
    public async Task PeerWithoutMaintenanceTokenRejectsWithoutStorageAsync(string? peer)
    {
        var persistence = new Mock<IPersistenceContextProvider>(MockBehavior.Strict);

        var response = await InvokeResetAsync(persistence.Object, "true", peer).ConfigureAwait(false);

        Assert.That(response.Success, Is.False);
        Assert.That(response.Code, Is.EqualTo("forbidden"));
        persistence.VerifyNoOtherCalls();
    }

    /// <summary>A wrong maintenance token is rejected even for a direct loopback peer.</summary>
    [Test]
    public async Task WrongMaintenanceTokenRejectsForLoopbackPeerAsync()
    {
        var persistence = new Mock<IPersistenceContextProvider>(MockBehavior.Strict);

        var response = await InvokeResetAsync(persistence.Object, "true", "127.0.0.1", token: "not-the-token").ConfigureAwait(false);

        Assert.That(response.Code, Is.EqualTo("forbidden"));
        persistence.VerifyNoOtherCalls();
    }

    /// <summary>The correct maintenance token authorizes a maintenance reset regardless of headers.</summary>
    [TestCase(null)]
    [TestCase("Forwarded")]
    [TestCase("X-Forwarded-For")]
    [TestCase("X-Real-IP")]
    [TestCase("CF-Connecting-IP")]
    public async Task MaintenanceTokenAuthorizesResetRegardlessOfHeaders(string? header)
    {
        var account = new Account { LoginName = "solotest", PasswordHash = BCrypt.Net.BCrypt.HashPassword("previous-password"), RecoveryCodeHash = "previous-recovery" };
        var playerContext = new Mock<IPlayerContext>();
        playerContext.Setup(context => context.GetAccountByLoginNameAsync("solotest", default)).ReturnsAsync(account);
        playerContext.Setup(context => context.SaveChangesAsync(default)).ReturnsAsync(true);
        var persistence = CreateProvider(playerContext.Object);

        var response = await InvokeResetAsync(persistence, "true", "203.0.113.20", header, "203.0.113.20", token: "test-token").ConfigureAwait(false);

        Assert.That(response.Success, Is.True);
        Assert.That(response.Code, Is.EqualTo("ok"));
        playerContext.Verify(context => context.SaveChangesAsync(default), Times.Once);
    }

    /// <summary>Maintenance revocation is atomic even when the loaded tracked account would have a stale null hash.</summary>
    [TestCase(true)]
    [TestCase(false)]
    public async Task MaintenanceUsesAtomicCredentialRevocationAsync(bool wins)
    {
        var snapshot = new AccountCredentialSnapshot("solotest", BCrypt.Net.BCrypt.HashPassword("previous-password"), null);
        var player = new Mock<IPlayerContext>();
        var credentials = player.As<IAccountCredentialContext>();
        credentials.Setup(context => context.ReadCredentialsAsync("solotest", default)).ReturnsAsync(snapshot);
        credentials.Setup(context => context.TryReplaceCredentialsAsync(snapshot, It.IsAny<string>(), null, default)).ReturnsAsync(wins);

        var response = await InvokeResetAsync(CreateProvider(player.Object), "true", "127.0.0.1", token: "test-token").ConfigureAwait(false);

        Assert.That(response.Success, Is.EqualTo(wins));
        credentials.Verify(context => context.TryReplaceCredentialsAsync(snapshot, It.IsAny<string>(), null, default), Times.Once);
        player.Verify(context => context.SaveChangesAsync(default), Times.Never);
        player.Verify(context => context.GetAccountByLoginNameAsync(It.IsAny<string>(), default), Times.Never);
    }

    /// <summary>The page always requires ownership proof; local maintenance configuration does not remove it.</summary>
    [TestCase(null, false)]
    [TestCase("false", false)]
    [TestCase("true", true)]
    public void ResetPageAlwaysRequiresRecoveryCode(string? setting, bool enabled)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IConfiguration>(CreateConfiguration(setting));
        context.Services.AddSingleton(CreateLocalizer());

        var page = context.Render<ResetPassword>();

        Assert.That(page.FindAll("form"), Has.Count.EqualTo(2));
        Assert.That(page.FindAll("#reset-recovery-code"), Has.Count.EqualTo(1));
        Assert.That(page.FindAll("#issue-password"), Has.Count.EqualTo(1));
        Assert.That(page.FindAll("a[href='change-password']"), Has.Count.EqualTo(1));
        Assert.That(page.FindAll("a[href='login']"), Has.Count.EqualTo(1));
        if (!enabled)
        {
            Assert.That(page.FindAll("[role='status']"), Is.Empty);
        }
        else
        {
            Assert.That(page.Find("[role='status']").TextContent, Does.Contain("LocalPasswordResetWarning"));
        }
    }

    /// <summary>A public proxied request succeeds only with a valid owned code, then returns the replacement.</summary>
    [Test]
    public async Task PublicRecoveryRequiresCodeAndNeverUsesUnconditionalSaveAsync()
    {
        var code = AccountRecoveryService.GenerateCode();
        _ = AccountRecoveryService.TryHashCode(code, out var hash);
        var snapshot = new AccountCredentialSnapshot("solotest", BCrypt.Net.BCrypt.HashPassword("old-password"), hash);
        var player = new Mock<IPlayerContext>();
        var credentials = player.As<IAccountCredentialContext>();
        credentials.Setup(context => context.ReadCredentialsAsync("solotest", default)).ReturnsAsync(snapshot);
        string? writtenPassword = null;
        string? writtenRecovery = null;
        credentials.Setup(context => context.TryReplaceCredentialsAsync(snapshot, It.IsAny<string>(), It.IsAny<string>(), default))
            .Callback<AccountCredentialSnapshot, string, string, CancellationToken>((_, password, recovery, _) =>
            {
                writtenPassword = password;
                writtenRecovery = recovery;
            }).ReturnsAsync(true);
        var persistence = CreateProvider(player.Object);

        var response = await InvokeAccountEndpointAsync(persistence, "/reset-password",
            ValidRequest with { RecoveryCode = code }, "false", "203.0.113.20", "X-Forwarded-For").ConfigureAwait(false);

        Assert.That(response.Success, Is.True);
        Assert.That(response.RecoveryCode, Is.Not.Null);
        Assert.That(BCrypt.Net.BCrypt.Verify(ValidRequest.NewPassword, writtenPassword!), Is.True);
        _ = AccountRecoveryService.TryHashCode(response.RecoveryCode, out var replacementHash);
        Assert.That(writtenRecovery, Is.EqualTo(replacementHash));
        Assert.That(writtenRecovery, Is.Not.EqualTo(hash));
        player.Verify(context => context.SaveChangesAsync(default), Times.Never);
        player.Verify(context => context.GetAccountByLoginNameAsync(It.IsAny<string>(), default), Times.Never);
    }

    /// <summary>Malformed public credentials are rejected before any storage context is created.</summary>
    [Test]
    public async Task MalformedPublicRecoveryRejectsBeforeStorageAsync()
    {
        var persistence = new Mock<IPersistenceContextProvider>(MockBehavior.Strict);
        var response = await InvokeAccountEndpointAsync(persistence.Object, "/reset-password",
            ValidRequest with { RecoveryCode = "123456" }, "false", "203.0.113.20").ConfigureAwait(false);
        Assert.That(response.Code, Is.EqualTo("invalid_recovery_code"));
        Assert.That(response.RecoveryCode, Is.Null);
        persistence.VerifyNoOtherCalls();
    }

    /// <summary>Old accounts and unknown accounts have the same public failure; no code means no ownership.</summary>
    [TestCase(true)]
    [TestCase(false)]
    public async Task UnknownOrUnenrolledAccountCannotRecoverAsync(bool exists)
    {
        var player = new Mock<IPlayerContext>();
        var credentials = player.As<IAccountCredentialContext>();
        credentials.Setup(context => context.ReadCredentialsAsync("solotest", default)).ReturnsAsync(exists
            ? new AccountCredentialSnapshot("solotest", BCrypt.Net.BCrypt.HashPassword("old-password"), null)
            : null);
        var response = await InvokeAccountEndpointAsync(CreateProvider(player.Object), "/reset-password",
            ValidRequest with { RecoveryCode = AccountRecoveryService.GenerateCode() }, "false", "203.0.113.20").ConfigureAwait(false);
        Assert.That(response.Code, Is.EqualTo("invalid_recovery_code"));
        Assert.That(response.RecoveryCode, Is.Null);
        credentials.Verify(context => context.TryReplaceCredentialsAsync(It.IsAny<AccountCredentialSnapshot>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        player.Verify(context => context.SaveChangesAsync(default), Times.Never);
    }

    /// <summary>A caller cannot enroll a legacy account by username alone.</summary>
    [Test]
    public async Task IssueRequiresCurrentPasswordBeforeStorageAsync()
    {
        var persistence = new Mock<IPersistenceContextProvider>(MockBehavior.Strict);
        var response = await InvokeAccountEndpointAsync(persistence.Object, "/recovery-code",
            new AccountRecoveryCodeRequest("solotest", null), null, "203.0.113.20").ConfigureAwait(false);
        Assert.That(response.Code, Is.EqualTo("bad_credentials"));
        Assert.That(response.RecoveryCode, Is.Null);
        persistence.VerifyNoOtherCalls();
    }

    /// <summary>Existing accounts can enroll remotely after current-password verification, without changing the password.</summary>
    [Test]
    public async Task LegacyAccountCanIssueCodeAfterCurrentPasswordAsync()
    {
        var snapshot = new AccountCredentialSnapshot("solotest", BCrypt.Net.BCrypt.HashPassword("old-password"), null);
        var player = new Mock<IPlayerContext>();
        var credentials = player.As<IAccountCredentialContext>();
        credentials.Setup(context => context.ReadCredentialsAsync("solotest", default)).ReturnsAsync(snapshot);
        credentials.Setup(context => context.TryReplaceCredentialsAsync(snapshot, snapshot.PasswordHash, It.IsAny<string>(), default)).ReturnsAsync(true);
        var response = await InvokeAccountEndpointAsync(CreateProvider(player.Object), "/recovery-code",
            new AccountRecoveryCodeRequest("solotest", "old-password"), null, "203.0.113.20").ConfigureAwait(false);
        Assert.That(response.Success, Is.True);
        Assert.That(AccountRecoveryService.TryHashCode(response.RecoveryCode, out _), Is.True);
        credentials.Verify(context => context.TryReplaceCredentialsAsync(snapshot, snapshot.PasswordHash, It.IsAny<string>(), default), Times.Once);
        player.Verify(context => context.SaveChangesAsync(default), Times.Never);
    }

    /// <summary>The legacy registration request still works and stores only the new recovery hash, not the secret code.</summary>
    [Test]
    public async Task RegistrationStoresHashAndKeepsSecurityCodeSemanticsAsync()
    {
        var account = new Account();
        var player = new Mock<IPlayerContext>();
        _ = player.As<IAccountCredentialContext>();
        player.Setup(context => context.CreateNew<Account>(It.IsAny<object?[]>())).Returns(account);
        player.Setup(context => context.SaveChangesAsync(default)).ReturnsAsync(true);
        var response = await InvokeAccountEndpointAsync(CreateProvider(player.Object), "/create",
            new AccountRegistrationRequest("solotest", "old-password", "old-password", "123456"), null, "203.0.113.20").ConfigureAwait(false);
        Assert.That(response.Success, Is.True);
        Assert.That(AccountRecoveryService.TryHashCode(response.RecoveryCode, out var hash), Is.True);
        Assert.That(account.RecoveryCodeHash, Is.EqualTo(hash));
        Assert.That(account.RecoveryCodeHash, Is.Not.EqualTo(response.RecoveryCode));
        // The security code is stored as a BCrypt hash, not in plain text.
        Assert.That(account.SecurityCode, Is.Not.EqualTo("123456"));
        Assert.That(BCrypt.Net.BCrypt.Verify("123456", account.SecurityCode), Is.True);
        Assert.That(BCrypt.Net.BCrypt.Verify("old-password", account.PasswordHash), Is.True);
    }

    /// <summary>Storage without atomic replacement never issues a misleading recovery credential.</summary>
    [Test]
    public async Task UnsupportedStorageCannotRecoverAsync()
    {
        var player = new Mock<IPlayerContext>();
        var response = await InvokeAccountEndpointAsync(CreateProvider(player.Object), "/reset-password",
            ValidRequest with { RecoveryCode = AccountRecoveryService.GenerateCode() }, null, "203.0.113.20").ConfigureAwait(false);
        Assert.That(response.Code, Is.EqualTo("recovery_unavailable"));
        Assert.That(response.RecoveryCode, Is.Null);
        player.Verify(context => context.SaveChangesAsync(default), Times.Never);
    }

    /// <summary>Explicitly enabled direct loopback maintenance retains the existing password-hash behavior.</summary>
    [TestCase("127.0.0.1")]
    [TestCase("::1")]
    [TestCase("::ffff:127.0.0.1")]
    public async Task ExplicitDirectLoopbackMaintenanceCanResetAsync(string peer)
    {
        var account = new Account { LoginName = "solotest", PasswordHash = BCrypt.Net.BCrypt.HashPassword("previous-password"), RecoveryCodeHash = "previous-recovery" };
        var playerContext = new Mock<IPlayerContext>();
        playerContext.Setup(context => context.GetAccountByLoginNameAsync("solotest", default)).ReturnsAsync(account);
        playerContext.Setup(context => context.SaveChangesAsync(default)).ReturnsAsync(true);
        var persistence = CreateProvider(playerContext.Object);

        var response = await InvokeResetAsync(persistence, "true", peer, token: "test-token").ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(response.Success, Is.True);
            Assert.That(response.Code, Is.EqualTo("ok"));
            Assert.That(BCrypt.Net.BCrypt.Verify("new-test-password", account.PasswordHash), Is.True);
            Assert.That(BCrypt.Net.BCrypt.Verify("previous-password", account.PasswordHash), Is.False);
            Assert.That(account.RecoveryCodeHash, Is.Null);
        });
        playerContext.Verify(context => context.SaveChangesAsync(default), Times.Once);
    }

    private static IPersistenceContextProvider CreateProvider(IPlayerContext player)
    {
        var configuration = new GameConfiguration();
        var configurationContext = new Mock<IConfigurationContext>();
        configurationContext.Setup(context => context.GetAsync<GameConfiguration>(default))
            .ReturnsAsync(new[] { configuration }.AsEnumerable());
        var persistence = new Mock<IPersistenceContextProvider>();
        persistence.Setup(provider => provider.CreateNewConfigurationContext()).Returns(configurationContext.Object);
        persistence.Setup(provider => provider.CreateNewPlayerContext(configuration)).Returns(player);
        return persistence.Object;
    }

    private static IConfigurationRoot CreateConfiguration(string? setting, string? maintenanceTokenFile = null) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            [SettingName] = setting,
            [TokenFileSettingName] = maintenanceTokenFile,
        })
        .Build();

    private static IStringLocalizer<SelfServiceResources> CreateLocalizer()
    {
        var localizer = new Mock<IStringLocalizer<SelfServiceResources>>();
        localizer.Setup(text => text[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key, resourceNotFound: true));
        return localizer.Object;
    }

    private static Task<AccountRegistrationResponse> InvokeResetAsync(
        IPersistenceContextProvider persistence,
        string? setting,
        string? peer,
        string? header = null,
        string headerValue = "203.0.113.20",
        string? token = null)
        => InvokeAccountEndpointAsync(persistence, "/reset-password", ValidRequest, setting, peer, header, headerValue, token);

    private static async Task<AccountRegistrationResponse> InvokeAccountEndpointAsync(
        IPersistenceContextProvider persistence,
        string route,
        object request,
        string? setting,
        string? peer,
        string? header = null,
        string headerValue = "203.0.113.20",
        string? maintenanceToken = null)
    {
        // A per-test token file keeps the guard away from any real server data.
        var tokenDirectory = Path.Combine(Path.GetTempPath(), "openmu-registration-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tokenDirectory);
        var tokenFile = Path.Combine(tokenDirectory, "maintenance-token.txt");
        File.WriteAllText(tokenFile, "test-token");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = Path.GetTempPath(),
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddConfiguration(CreateConfiguration(setting, tokenFile));
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(persistence);
        builder.Services.AddSingleton(CreateLocalizer());
        builder.Services.AddAccountSelfServiceGuard();
        await using var app = builder.Build();
        app.MapPublicRegistrationEndpoints();
        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == "/api/registration" + route);

        object body = request;
        if (maintenanceToken is not null)
        {
            // The guard compares the request token against the file, so the test
            // can hand in either the right value or a wrong one.
            var node = JsonSerializer.SerializeToNode(request, request.GetType())!.AsObject();
            if (maintenanceToken != "test-token")
            {
                File.WriteAllText(tokenFile, "a-different-token");
            }

            node["maintenanceToken"] = maintenanceToken;
            body = node;
        }

        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(body);
        using var requestBody = new MemoryStream(requestBytes);
        using var responseBody = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = app.Services };
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetectionFeature());
        context.Connection.RemoteIpAddress = peer is null ? null : IPAddress.Parse(peer);
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/registration" + route;
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = requestBytes.Length;
        context.Request.Body = requestBody;
        context.Response.Body = responseBody;
        if (header is not null)
        {
            context.Request.Headers[header] = headerValue;
        }

        await endpoint.RequestDelegate!(context).ConfigureAwait(false);
        Assert.That(context.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
        responseBody.Position = 0;
        var response = await JsonSerializer.DeserializeAsync<AccountRegistrationResponse>(
            responseBody, new JsonSerializerOptions(JsonSerializerDefaults.Web)).ConfigureAwait(false);
        Assert.That(response, Is.Not.Null);
        return response!;
    }

    private sealed class BodyDetectionFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }
}
