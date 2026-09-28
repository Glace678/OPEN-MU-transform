// <copyright file="PublicRegistrationEndpoints.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.Properties;

/// <summary>
/// Maps the account self-service endpoints used by the game client:
/// registration, password change, recovery-code ownership and local maintenance resets.
/// Registration is anonymous, password changes require the current password,
/// and public recovery requires an independent one-time credential. Resets without
/// an ownership credential are disabled by default.
/// </summary>
public static class PublicRegistrationEndpoints
{
    // The classic login server only accepts 3-10 ASCII letters or digits.
    private static readonly Regex ValidLoginName = new("^[A-Za-z0-9]{3,10}$", RegexOptions.Compiled);

    /// <summary>Maps the public account API without authorization.</summary>
    /// <param name="endpoints">The routes which will receive the account API.</param>
    /// <returns>The supplied builder, to allow further endpoint mappings.</returns>
    public static IEndpointRouteBuilder MapPublicRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/registration").AllowAnonymous();
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            context.HttpContext.Response.Headers.Pragma = "no-cache";
            return await next(context).ConfigureAwait(false);
        });

        group.MapPost("/create", CreateAccountAsync).DisableAntiforgery();
        group.MapPost("/change-password", ChangePasswordAsync).DisableAntiforgery();
        group.MapPost("/reset-password", ResetPasswordAsync).DisableAntiforgery();
        group.MapPost("/recovery-code", IssueRecoveryCodeAsync).DisableAntiforgery();
        group.MapGet("/text", GetPublicText);

        return endpoints;
    }

    private static IResult GetPublicText(string? culture, IStringLocalizer<SelfServiceResources> text)
    {
        var supported = new[] { "en", "zh-CN", "zh-TW", "ja", "ko", "de", "es", "fr", "pt", "ru", "uk", "pl", "id", "vi", "tl" };
        var selected = supported.FirstOrDefault(value => value.Equals(culture, StringComparison.OrdinalIgnoreCase)) ?? "en";
        using var scope = CultureHelper.SetTemporaryCulture(CultureInfo.GetCultureInfo(selected));
        var keys = new[]
        {
            "GameRegistrationHeading", "AccountNameLabel", "AccountNameHelp", "PasswordLabel", "PasswordHelp",
            "ConfirmPasswordLabel", "SecurityCodeLabel", "SecurityCodeHelp", "RegisterButton", "ChangePasswordLink",
            "ForgotPasswordLink", "ChangePasswordHeading", "CurrentPasswordLabel", "NewPasswordLabel",
            "ConfirmNewPasswordLabel", "ChangePasswordButton", "ResetPasswordHeading", "ResetPasswordButton",
            "CannotConnect", "InvalidResponse", "RecoveryCodeLabel", "RecoveryCodeNotice", "RecoveryIssueTitle",
            "IssueRecoveryCodeButton", "RecoveryUnavailable", "PasswordMismatch",
        };
        return Results.Ok(keys.ToDictionary(key => key, key => text[key].Value, StringComparer.Ordinal));
    }

    private static async Task<IResult> CreateAccountAsync(
        AccountRegistrationRequest? request,
        IPersistenceContextProvider persistenceContextProvider,
        ILoggerFactory loggerFactory,
        IStringLocalizer<SelfServiceResources> text)
    {
        var logger = loggerFactory.CreateLogger("MUnique.OpenMU.PublicRegistration");
        var loginName = (request?.LoginName ?? string.Empty).Trim();
        var password = request?.Password ?? string.Empty;
        var confirmedPassword = request?.ConfirmPassword ?? string.Empty;

        // Keep the request backward-compatible: older clients do not send a security
        // code, and character actions already fall back to the account password when
        // the persisted field is empty.
        var securityCode = string.IsNullOrEmpty(request?.SecurityCode)
            ? string.Empty
            : (request?.SecurityCode ?? string.Empty).Trim();

        if (!ValidLoginName.IsMatch(loginName))
        {
            return Results.Ok(new AccountRegistrationResponse(false, "invalid_name", text["InvalidName"].Value));
        }

        if (!IsValidPassword(password, out var passwordError))
        {
            return Results.Ok(new AccountRegistrationResponse(false, passwordError, PasswordMessage(passwordError, text)));
        }

        if (password != confirmedPassword)
        {
            return Results.Ok(new AccountRegistrationResponse(false, "password_mismatch", text["PasswordMismatch"].Value));
        }

        if (!string.IsNullOrEmpty(securityCode) && !IsValidSecurityCode(securityCode))
        {
            return Results.Ok(new AccountRegistrationResponse(false, "invalid_security_code", text["InvalidSecurityCode"].Value));
        }

        using var configurationContext = persistenceContextProvider.CreateNewConfigurationContext();
        var configurations = await configurationContext.GetAsync<GameConfiguration>().ConfigureAwait(false);
        var configuration = configurations.FirstOrDefault();
        if (configuration is null)
        {
            logger.LogError("Self-registration failed: no game configuration exists yet.");
            return Results.Ok(new AccountRegistrationResponse(false, "error", text["ServerNotInitialized"].Value));
        }

        using var context = persistenceContextProvider.CreateNewPlayerContext(configuration);
        Account? account = null;
        try
        {
            if (await context.GetAccountByLoginNameAsync(loginName).ConfigureAwait(false) is not null)
            {
                return Results.Ok(new AccountRegistrationResponse(false, "duplicate", text["DuplicateAccount"].Value));
            }

            if (context.HasChanges && !await context.SaveChangesAsync().ConfigureAwait(false))
            {
                return Results.Ok(new AccountRegistrationResponse(false, "error", text["ServerBusy"].Value));
            }

            account = context.CreateNew<Account>();
            account.LoginName = loginName;
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);

            // Unsupported storage may still register legacy accounts, but must not
            // issue a recovery credential which it cannot consume atomically.
            var recoveryCode = context is IAccountCredentialContext ? AccountRecoveryService.GenerateCode() : null;
            if (recoveryCode is not null)
            {
                _ = AccountRecoveryService.TryHashCode(recoveryCode, out var recoveryHash);
                account.RecoveryCodeHash = recoveryHash;
            }

            account.SecurityCode = securityCode;
            account.State = AccountState.Normal;
            account.LanguageIsoCode = GetAccountLanguageIsoCode();
            account.RegistrationDate = DateTime.UtcNow;

            if (!await context.SaveChangesAsync().ConfigureAwait(false))
            {
                throw new InvalidOperationException("The account context did not confirm the save.");
            }

            logger.LogInformation("Self-registered account {LoginName}.", loginName);
            return Results.Ok(new AccountRegistrationResponse(true, "ok", text["RegistrationSuccess"].Value, recoveryCode));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Self-registration for {LoginName} failed.", loginName);
            if (account is not null)
            {
                try
                {
                    context.Detach(account);
                }
                catch (Exception cleanupError)
                {
                    logger.LogError(cleanupError, "Could not detach the unsaved account.");
                }
            }

            return Results.Ok(new AccountRegistrationResponse(false, "error", text["RegistrationFailed"].Value));
        }
    }

    private static async Task<IResult> ChangePasswordAsync(
        PasswordChangeRequest? request,
        IPersistenceContextProvider persistenceContextProvider,
        ILoggerFactory loggerFactory,
        IStringLocalizer<SelfServiceResources> text)
    {
        var logger = loggerFactory.CreateLogger("MUnique.OpenMU.PublicRegistration");
        var loginName = (request?.LoginName ?? string.Empty).Trim();
        var oldPassword = request?.OldPassword ?? string.Empty;
        var newPassword = request?.NewPassword ?? string.Empty;
        var confirmedPassword = request?.ConfirmNewPassword ?? string.Empty;

        if (!ValidLoginName.IsMatch(loginName))
        {
            return Results.Ok(new AccountRegistrationResponse(false, "invalid_name", text["InvalidName"].Value));
        }

        if (string.IsNullOrEmpty(oldPassword))
        {
            return Results.Ok(new AccountRegistrationResponse(false, "invalid_old_password", text["InvalidOldPassword"].Value));
        }

        if (!IsValidPassword(newPassword, out var passwordError))
        {
            return Results.Ok(new AccountRegistrationResponse(false, passwordError, PasswordMessage(passwordError, text)));
        }

        if (newPassword != confirmedPassword)
        {
            return Results.Ok(new AccountRegistrationResponse(false, "password_mismatch", text["PasswordMismatch"].Value));
        }

        using var configurationContext = persistenceContextProvider.CreateNewConfigurationContext();
        try
        {
            using var context = await CreatePlayerContextAsync(configurationContext, persistenceContextProvider).ConfigureAwait(false);
            var account = await context.GetAccountByLoginNameAsync(loginName).ConfigureAwait(false);
            if (account is null || !BCrypt.Net.BCrypt.Verify(oldPassword, account.PasswordHash))
            {
                return Results.Ok(new AccountRegistrationResponse(false, "bad_credentials", text["BadCredentials"].Value));
            }

            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            if (!await context.SaveChangesAsync().ConfigureAwait(false))
            {
                return Results.Ok(new AccountRegistrationResponse(false, "error", text["ServerBusy"].Value));
            }

            logger.LogInformation("Password changed for account {LoginName}.", loginName);
            return Results.Ok(new AccountRegistrationResponse(true, "ok", text["PasswordChangeSuccess"].Value));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Password change for {LoginName} failed.", loginName);
            return Results.Ok(new AccountRegistrationResponse(false, "error", text["PasswordChangeFailed"].Value));
        }
    }

    private static async Task<IResult> ResetPasswordAsync(
        HttpContext httpContext,
        PasswordResetRequest? request,
        IPersistenceContextProvider persistenceContextProvider,
        ILoggerFactory loggerFactory,
        IStringLocalizer<SelfServiceResources> text,
        IConfiguration configuration)
    {
        var logger = loggerFactory.CreateLogger("MUnique.OpenMU.PublicRegistration");
        var recoveryCode = request?.RecoveryCode ?? string.Empty;
        var hasRecoveryCode = !string.IsNullOrWhiteSpace(recoveryCode);

        // A loopback peer is not proof of account ownership: local reverse proxies
        // can represent remote callers. This legacy maintenance path must be opt-in.
        if (!hasRecoveryCode)
        {
            if (!bool.TryParse(configuration["AccountSelfService:AllowLocalPasswordReset"], out var allowLocalReset)
                || !allowLocalReset)
            {
                var disabled = text["ResetDisabled"];
                var message = disabled.ResourceNotFound
                    ? "Recovery without an ownership credential is disabled. Use your recovery code or contact an administrator."
                    : disabled.Value;
                return Results.Ok(new AccountRegistrationResponse(false, "reset_disabled", message));
            }

            var remoteIp = httpContext.Connection.RemoteIpAddress;
            if (remoteIp?.IsIPv4MappedToIPv6 == true)
            {
                remoteIp = remoteIp.MapToIPv4();
            }

            if (remoteIp is null || !IPAddress.IsLoopback(remoteIp) || HasProxyHeaders(httpContext.Request.Headers))
            {
                return Results.Ok(new AccountRegistrationResponse(false, "forbidden", text["ResetForbidden"].Value));
            }
        }
        else if (!AccountRecoveryService.TryHashCode(recoveryCode, out _))
        {
            return Results.Ok(new AccountRegistrationResponse(false, "invalid_recovery_code", text["InvalidRecoveryCode"].Value));
        }

        var loginName = (request?.LoginName ?? string.Empty).Trim();
        var newPassword = request?.NewPassword ?? string.Empty;
        var confirmedPassword = request?.ConfirmNewPassword ?? string.Empty;

        if (!ValidLoginName.IsMatch(loginName))
        {
            return Results.Ok(new AccountRegistrationResponse(false, "invalid_name", text["InvalidName"].Value));
        }

        if (!IsValidPassword(newPassword, out var passwordError))
        {
            return Results.Ok(new AccountRegistrationResponse(false, passwordError, PasswordMessage(passwordError, text)));
        }

        if (newPassword != confirmedPassword)
        {
            return Results.Ok(new AccountRegistrationResponse(false, "password_mismatch", text["PasswordMismatch"].Value));
        }

        using var configurationContext = persistenceContextProvider.CreateNewConfigurationContext();
        try
        {
            using var context = await CreatePlayerContextAsync(configurationContext, persistenceContextProvider).ConfigureAwait(false);
            if (hasRecoveryCode)
            {
                if (context is not IAccountCredentialContext credentials)
                {
                    return Results.Ok(new AccountRegistrationResponse(false, "recovery_unavailable", text["RecoveryUnavailable"].Value));
                }

                var replacement = await AccountRecoveryService.ResetAsync(credentials, loginName, recoveryCode, newPassword).ConfigureAwait(false);
                if (replacement is null)
                {
                    return Results.Ok(new AccountRegistrationResponse(false, "invalid_recovery_code", text["InvalidRecoveryCode"].Value));
                }

                logger.LogInformation("Password recovered with an owned one-time code for account {LoginName}.", loginName);
                return Results.Ok(new AccountRegistrationResponse(true, "ok", text["PasswordResetSuccess"].Value, replacement));
            }

            if (context is IAccountCredentialContext maintenanceCredentials)
            {
                var snapshot = await maintenanceCredentials.ReadCredentialsAsync(loginName).ConfigureAwait(false);
                if (snapshot is null)
                {
                    return Results.Ok(new AccountRegistrationResponse(false, "not_found", text["AccountNotFound"].Value));
                }

                if (!await maintenanceCredentials.TryReplaceCredentialsAsync(snapshot, BCrypt.Net.BCrypt.HashPassword(newPassword), null).ConfigureAwait(false))
                {
                    return Results.Ok(new AccountRegistrationResponse(false, "error", text["ServerBusy"].Value));
                }

                logger.LogInformation("Password reset (explicit local maintenance) for account {LoginName}.", loginName);
                return Results.Ok(new AccountRegistrationResponse(true, "ok", text["PasswordResetSuccess"].Value));
            }

            var account = await context.GetAccountByLoginNameAsync(loginName).ConfigureAwait(false);
            if (account is null)
            {
                return Results.Ok(new AccountRegistrationResponse(false, "not_found", text["AccountNotFound"].Value));
            }

            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            account.RecoveryCodeHash = null;
            if (!await context.SaveChangesAsync().ConfigureAwait(false))
            {
                return Results.Ok(new AccountRegistrationResponse(false, "error", text["ServerBusy"].Value));
            }

            logger.LogInformation("Password reset (explicit local maintenance) for account {LoginName}.", loginName);
            return Results.Ok(new AccountRegistrationResponse(true, "ok", text["PasswordResetSuccess"].Value));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Password reset for {LoginName} failed.", loginName);
            return Results.Ok(new AccountRegistrationResponse(false, "error", text["PasswordResetFailed"].Value));
        }
    }

    private static async Task<IResult> IssueRecoveryCodeAsync(
        AccountRecoveryCodeRequest? request,
        IPersistenceContextProvider persistenceContextProvider,
        ILoggerFactory loggerFactory,
        IStringLocalizer<SelfServiceResources> text)
    {
        var loginName = (request?.LoginName ?? string.Empty).Trim();
        var currentPassword = request?.CurrentPassword ?? string.Empty;
        if (!ValidLoginName.IsMatch(loginName) || !IsValidPassword(currentPassword, out _))
        {
            return Results.Ok(new AccountRegistrationResponse(false, "bad_credentials", text["BadCredentials"].Value));
        }

        var logger = loggerFactory.CreateLogger("MUnique.OpenMU.PublicRegistration");
        using var configurationContext = persistenceContextProvider.CreateNewConfigurationContext();
        try
        {
            using var context = await CreatePlayerContextAsync(configurationContext, persistenceContextProvider).ConfigureAwait(false);
            if (context is not IAccountCredentialContext credentials)
            {
                return Results.Ok(new AccountRegistrationResponse(false, "recovery_unavailable", text["RecoveryUnavailable"].Value));
            }

            var replacement = await AccountRecoveryService.IssueAsync(credentials, loginName, currentPassword).ConfigureAwait(false);
            if (replacement is null)
            {
                return Results.Ok(new AccountRegistrationResponse(false, "bad_credentials", text["BadCredentials"].Value));
            }

            logger.LogInformation("Recovery code issued after password verification for account {LoginName}.", loginName);
            return Results.Ok(new AccountRegistrationResponse(true, "ok", text["RecoveryIssueSuccess"].Value, replacement));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Recovery code issue failed for account {LoginName}.", loginName);
            return Results.Ok(new AccountRegistrationResponse(false, "error", text["RecoveryIssueFailed"].Value));
        }
    }

    private static bool HasProxyHeaders(IHeaderDictionary headers) => headers.Keys.Any(name =>
        name.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("X-Original-", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Forwarded", StringComparison.OrdinalIgnoreCase)
        || name.Equals("X-Real-IP", StringComparison.OrdinalIgnoreCase)
        || name.Equals("X-Client-IP", StringComparison.OrdinalIgnoreCase)
        || name.Equals("True-Client-IP", StringComparison.OrdinalIgnoreCase)
        || name.Equals("CF-Connecting-IP", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Fastly-Client-IP", StringComparison.OrdinalIgnoreCase));

    private static bool IsValidPassword(string password, out string errorCode)
    {
        if (password.Length is < 3 or > 20)
        {
            errorCode = "invalid_password";
            return false;
        }

        if (password.Any(c => c < 0x21 || c > 0x7e))
        {
            errorCode = "invalid_password_chars";
            return false;
        }

        errorCode = string.Empty;
        return true;
    }

    private static bool IsValidSecurityCode(string securityCode) =>
        securityCode.Length is >= 3 and <= 10
        && securityCode.All(c => c is >= '!' and <= '~');

    private static string GetAccountLanguageIsoCode()
    {
        var isoCode = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return isoCode is { Length: > 0 and <= 3 } ? isoCode : "en";
    }

    private static string PasswordMessage(string errorCode, IStringLocalizer<SelfServiceResources> text) => errorCode switch
    {
        "invalid_password" => text["InvalidPassword"].Value,
        "invalid_password_chars" => text["InvalidPasswordChars"].Value,
        _ => text["InvalidPassword"].Value,
    };

    private static async Task<IPlayerContext> CreatePlayerContextAsync(
        IContext configurationContext,
        IPersistenceContextProvider persistenceContextProvider)
    {
        var configurations = await configurationContext.GetAsync<GameConfiguration>().ConfigureAwait(false);
        var configuration = configurations.FirstOrDefault()
            ?? throw new InvalidOperationException("No game configuration exists yet.");
        return persistenceContextProvider.CreateNewPlayerContext(configuration);
    }
}
