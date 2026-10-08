// <copyright file="PublicRegistrationEndpoints.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

using System.Globalization;
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
/// an ownership credential are disabled by default; when enabled they require the
/// server's maintenance token instead of trusting the caller's network position.
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

        group.MapPost("/create", CreateAccountAsync)
            .RequireRateLimiting(AccountSelfServicePolicies.Registration)
            .DisableAntiforgery();
        group.MapPost("/change-password", ChangePasswordAsync)
            .RequireRateLimiting(AccountSelfServicePolicies.CredentialVerification)
            .DisableAntiforgery();
        group.MapPost("/reset-password", ResetPasswordAsync)
            .RequireRateLimiting(AccountSelfServicePolicies.CredentialVerification)
            .DisableAntiforgery();
        group.MapPost("/recovery-code", IssueRecoveryCodeAsync)
            .RequireRateLimiting(AccountSelfServicePolicies.CredentialVerification)
            .DisableAntiforgery();
        group.MapGet("/text", GetPublicText);

        return endpoints;
    }

    private static IResult GetPublicText(string? culture, IStringLocalizer<SelfServiceResources> text)
    {
        var selected = SupportedAccountCultures.FirstOrDefault(value => value.Equals(culture, StringComparison.OrdinalIgnoreCase)) ?? "en";
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
        HttpContext httpContext,
        AccountRegistrationRequest? request,
        AccountSelfServiceGuard guard,
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
        var securityCode = request?.SecurityCode?.Trim() ?? string.Empty;

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
                // Public deployments must not confirm the existence of an account.
                if (guard.IsPublicMode)
                {
                    return Results.Ok(new AccountRegistrationResponse(false, "error", text["RegistrationFailed"].Value));
                }

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

            account.SecurityCode = GameLogic.SecurityCodeSecurity.HashCode(securityCode);
            account.State = AccountState.Normal;
            account.LanguageIsoCode = GetAccountLanguageIsoCode(request, httpContext);
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

            // A concurrent insert of the same name surfaces as a unique constraint
            // violation rather than as a duplicate lookup; treat it as one.
            if (IsUniqueConstraintViolation(ex))
            {
                return guard.IsPublicMode
                    ? Results.Ok(new AccountRegistrationResponse(false, "error", text["RegistrationFailed"].Value))
                    : Results.Ok(new AccountRegistrationResponse(false, "duplicate", text["DuplicateAccount"].Value));
            }

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
        AccountSelfServiceGuard guard,
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

        if (guard.IsLockedOut(loginName))
        {
            return Results.Ok(new AccountRegistrationResponse(false, "bad_credentials", text["TooManyAttempts"].Value));
        }

        using var configurationContext = persistenceContextProvider.CreateNewConfigurationContext();
        try
        {
            using var context = await CreatePlayerContextAsync(configurationContext, persistenceContextProvider).ConfigureAwait(false);
            var account = await context.GetAccountByLoginNameAsync(loginName).ConfigureAwait(false);
            if (account is null)
            {
                // Do not count failures for non-existent accounts, otherwise anyone could
                // lock an arbitrary username for 15 minutes. The response stays identical
                // so account names cannot be enumerated.
                return Results.Ok(new AccountRegistrationResponse(false, "bad_credentials", text["BadCredentials"].Value));
            }

            if (!BCrypt.Net.BCrypt.Verify(oldPassword, account.PasswordHash))
            {
                guard.RegisterFailedAttempt(loginName);
                return Results.Ok(new AccountRegistrationResponse(false, "bad_credentials", text["BadCredentials"].Value));
            }

            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            if (!await context.SaveChangesAsync().ConfigureAwait(false))
            {
                return Results.Ok(new AccountRegistrationResponse(false, "error", text["ServerBusy"].Value));
            }

            guard.RegisterSuccessfulAttempt(loginName);
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
        AccountSelfServiceGuard guard,
        IPersistenceContextProvider persistenceContextProvider,
        ILoggerFactory loggerFactory,
        IStringLocalizer<SelfServiceResources> text,
        IConfiguration configuration)
    {
        var logger = loggerFactory.CreateLogger("MUnique.OpenMU.PublicRegistration");
        var recoveryCode = request?.RecoveryCode ?? string.Empty;
        var hasRecoveryCode = !string.IsNullOrWhiteSpace(recoveryCode);

        // Ownership without a recovery code is proven by the server's maintenance
        // token file. A loopback peer or a missing proxy header proves nothing:
        // local reverse proxies and tunnels present remote callers as loopback.
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

            if (!guard.IsMaintenanceTokenValid(request?.MaintenanceToken))
            {
                logger.LogWarning("Rejected a maintenance password reset from {RemoteIp} without a valid maintenance token.", httpContext.Connection.RemoteIpAddress);
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

        if (hasRecoveryCode && guard.IsLockedOut(loginName))
        {
            return Results.Ok(new AccountRegistrationResponse(false, "bad_credentials", text["TooManyAttempts"].Value));
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
                    // Only count it against an existing account; a non-existent account must
                    // not be lockable by name. The response is identical to avoid enumeration.
                    if (await credentials.ReadCredentialsAsync(loginName).ConfigureAwait(false) is not null)
                    {
                        guard.RegisterFailedAttempt(loginName);
                    }

                    return Results.Ok(new AccountRegistrationResponse(false, "invalid_recovery_code", text["InvalidRecoveryCode"].Value));
                }

                guard.RegisterSuccessfulAttempt(loginName);
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

                logger.LogInformation("Password reset (maintenance token) for account {LoginName}.", loginName);
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

            logger.LogInformation("Password reset (maintenance token) for account {LoginName}.", loginName);
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
        AccountSelfServiceGuard guard,
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

        if (guard.IsLockedOut(loginName))
        {
            return Results.Ok(new AccountRegistrationResponse(false, "bad_credentials", text["TooManyAttempts"].Value));
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
                // Only count it against an existing account; a non-existent account must
                // not be lockable by name. The response is identical to avoid enumeration.
                if (await credentials.ReadCredentialsAsync(loginName).ConfigureAwait(false) is not null)
                {
                    guard.RegisterFailedAttempt(loginName);
                }

                return Results.Ok(new AccountRegistrationResponse(false, "bad_credentials", text["BadCredentials"].Value));
            }

            guard.RegisterSuccessfulAttempt(loginName);
            logger.LogInformation("Recovery code issued after password verification for account {LoginName}.", loginName);
            return Results.Ok(new AccountRegistrationResponse(true, "ok", text["RecoveryIssueSuccess"].Value, replacement));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Recovery code issue failed for account {LoginName}.", loginName);
            return Results.Ok(new AccountRegistrationResponse(false, "error", text["RecoveryIssueFailed"].Value));
        }
    }

    private static bool IsValidPassword(string password, out string errorCode)
    {
        if (password.Length is < 8 or > 20)
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

    // Client-declared first: the server's UI culture says nothing about the player.
    // Unknown values fall back to Accept-Language, then to English.
    private static readonly string[] SupportedAccountCultures =
    {
        "en", "zh-CN", "zh-TW", "ja", "ko", "de", "es", "fr", "pt", "ru", "uk", "pl", "id", "vi", "tl",
    };

    private static string GetAccountLanguageIsoCode(AccountRegistrationRequest? request, HttpContext httpContext)
    {
        var requested = NormalizeCulture(request?.Culture) ?? NormalizeCulture(ReadAcceptLanguage(httpContext));
        if (requested is not null)
        {
            return requested;
        }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is { Length: > 0 and <= 3 } isoCode ? isoCode : "en";
    }

    private static string? ReadAcceptLanguage(HttpContext httpContext)
    {
        var header = httpContext.Request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        var preferred = header.Split(',')[0].Split(';')[0].Trim();
        return preferred.Length > 0 ? preferred : null;
    }

    private static string? NormalizeCulture(string? value)
    {
        var candidate = value?.Trim();
        if (string.IsNullOrEmpty(candidate))
        {
            return null;
        }

        foreach (var supported in SupportedAccountCultures)
        {
            if (string.Equals(candidate, supported, StringComparison.OrdinalIgnoreCase))
            {
                return supported;
            }
        }

        // "zh-Hans-CN" and similar tags: accept them when their language part is supported.
        var language = candidate.Split('-', '_')[0];
        foreach (var supported in SupportedAccountCultures)
        {
            if (string.Equals(language, supported, StringComparison.OrdinalIgnoreCase))
            {
                return supported;
            }
        }

        return null;
    }

    // A concurrent insert of the same login name surfaces as a unique constraint
    // violation (PostgreSQL 23505) rather than as a duplicate lookup. The provider
    // type is matched by name so this layer stays free of a database dependency.
    private static bool IsUniqueConstraintViolation(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.GetType().FullName != "Npgsql.PostgresException")
            {
                continue;
            }

            var state = current.GetType().GetProperty("SqlState")?.GetValue(current) as string;
            if (state == "23505")
            {
                return true;
            }
        }

        return false;
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
