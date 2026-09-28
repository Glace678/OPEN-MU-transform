// <copyright file="MobileGmEndpoints.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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

        return endpoints;
    }
}
