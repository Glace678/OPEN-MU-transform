// <copyright file="MobileGmEndpoints.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// Maps the narrowly scoped endpoints used by the packaged mobile GM application.
/// </summary>
public static class MobileGmEndpoints
{
    /// <summary>Maps the mobile GM API with its independent authentication policy.</summary>
    /// <param name="endpoints">The routes which will receive the mobile API.</param>
    /// <returns>The supplied builder, to allow further endpoint mappings.</returns>
    public static IEndpointRouteBuilder MapMobileGmEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/mobile-gm")
            .RequireAuthorization(MobileGmAuthenticationDefaults.Policy);

        // Require an explicit custom header on all state-changing mobile-gm POST requests so
        // cross-site browser form submissions cannot ride a shared SameSite=Lax cookie when
        // antiforgery is disabled.
        group.AddEndpointFilter(async (invocationContext, next) =>
        {
            var req = invocationContext.HttpContext.Request;
            if (HttpMethods.IsPost(req.Method)
                && !string.Equals(
                    req.Headers[MobileGmAuthenticationDefaults.ClientHeaderName],
                    MobileGmAuthenticationDefaults.ClientHeaderValue,
                    StringComparison.Ordinal))
            {
                return Results.Json(new { success = false, error = "missing-client-header" }, statusCode: StatusCodes.Status400BadRequest);
            }

            return await next(invocationContext).ConfigureAwait(false);
        });

        group.MapGet("/status", async (MobileGmService service) =>
                Results.Ok(await service.GetStatusAsync().ConfigureAwait(false)));
        group.MapGet("/items", (string? q, MobileGmService service) =>
                Results.Ok(service.SearchItems(q)));
        group.MapPost("/grant-item", async (MobileGmGrantRequest request, MobileGmService service) =>
                Results.Ok(await service.GrantItemAsync(request).ConfigureAwait(false)))
            .DisableAntiforgery();
        group.MapPost("/grant-zen", async (MobileGmZenRequest request, MobileGmService service) =>
                Results.Ok(await service.GrantZenAsync(request).ConfigureAwait(false)))
            .DisableAntiforgery();

        // F1 (M1-01 / M2-H1): the mobile GM app no longer presents a static,
        // APK-distributed bearer. It signs in here with an administrator account
        // (username + password + optional TOTP). On success this endpoint issues
        // the same cookie session the web panel uses; the grant endpoints then
        // require that session plus the Administrator role. Brute force on the
        // credentials is covered by ASP.NET Core Identity lockout (AccessFailedAsync /
        // IsLockedOutAsync) and by the local/LAN address guard below.
        group.MapPost("/login", async (MobileGmLoginRequest request, HttpContext httpContext, UserManager<AdminUser> userManager) =>
            {
                if (!MobileGmAuthenticationDefaults.IsAllowedDirectAddress(httpContext.Connection.RemoteIpAddress))
                {
                    return Results.Json(new { success = false, error = "untrusted-address" }, statusCode: StatusCodes.Status403Forbidden);
                }

                var loginName = (request.LoginName ?? string.Empty).Trim();
                var password = request.Password ?? string.Empty;
                if (loginName.Length == 0 || password.Length == 0)
                {
                    return Results.Json(new { success = false, error = "missing-credentials" }, statusCode: StatusCodes.Status400BadRequest);
                }

                var user = await userManager.FindByNameAsync(loginName).ConfigureAwait(false);
                if (user is null || user.IsDisabled)
                {
                    _ = await Task.Run(() => BCrypt.Net.BCrypt.Verify(password, PublicRegistrationEndpoints.DummyBcryptHash)).ConfigureAwait(false);
                    return Results.Json(new { success = false, error = "invalid-credentials" }, statusCode: StatusCodes.Status401Unauthorized);
                }

                if (await userManager.IsLockedOutAsync(user).ConfigureAwait(false))
                {
                    return Results.Json(new { success = false, error = "locked-out" }, statusCode: StatusCodes.Status423Locked);
                }

                if (!await userManager.CheckPasswordAsync(user, password).ConfigureAwait(false))
                {
                    await userManager.AccessFailedAsync(user).ConfigureAwait(false);
                    return Results.Json(new { success = false, error = "invalid-credentials" }, statusCode: StatusCodes.Status401Unauthorized);
                }

                if (user.IsTwoFactorEnabled)
                {
                    var code = (request.Code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
                    if (code.Length == 0)
                    {
                        // The client must re-submit with the TOTP/recovery code.
                        return Results.Json(new { success = false, twoFactorRequired = true });
                    }

                    var valid = await AdminLoginService.VerifySecondFactorOrRecoveryCodeAsync(userManager, user, code).ConfigureAwait(false);
                    if (!valid)
                    {
                        await userManager.AccessFailedAsync(user).ConfigureAwait(false);
                        return Results.Json(new { success = false, error = "invalid-code" }, statusCode: StatusCodes.Status401Unauthorized);
                    }
                }

                var claims = AdminLoginService.CreateClaims(user, user.IsTwoFactorEnabled);
                var isAdministrator = claims.Any(
                    claim => claim.Type == ClaimTypes.Role
                             && string.Equals(claim.Value, AdminRoles.Administrator, StringComparison.Ordinal));
                if (!isAdministrator)
                {
                    // The mobile GM write API requires an administrator; a viewer/operator
                    // must not be able to obtain a grant session even with valid credentials.
                    return Results.Json(new { success = false, error = "requires-administrator" }, statusCode: StatusCodes.Status403Forbidden);
                }

                await AdminLoginService.RecordSuccessfulLoginAsync(userManager, user).ConfigureAwait(false);
                claims = AdminLoginService.CreateClaims(user, user.IsTwoFactorEnabled);

                var identity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    ClaimTypes.Name,
                    ClaimTypes.Role);
                await httpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(identity),
                        new AuthenticationProperties { IsPersistent = false })
                    .ConfigureAwait(false);

                return Results.Json(new { success = true });
            })
            .AllowAnonymous()
            .RequireRateLimiting(MobileGmAuthenticationDefaults.LoginRateLimitPolicy)
            .DisableAntiforgery();

        group.MapPost("/logout", async (HttpContext httpContext) =>
            {
                await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
                return Results.NoContent();
            })
            .DisableAntiforgery();

        return endpoints;
    }

    /// <summary>The request body of the mobile GM login endpoint.</summary>
    /// <param name="LoginName">The administrator account login name.</param>
    /// <param name="Password">The administrator account password.</param>
    /// <param name="Code">The optional TOTP/recovery code (required when the account has 2FA enabled).</param>
    public record MobileGmLoginRequest(string? LoginName, string? Password, string? Code);
}
