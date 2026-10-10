// <copyright file="AdminAccessRequirement.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Auth;

using Microsoft.AspNetCore.Authorization;
using MUnique.OpenMU.Persistence.AdminAuth;

/// <summary>
/// The requirement to access the admin panel, optionally with a specific role.
/// </summary>
/// <param name="RequiredRole">The role which is required; <c>null</c>, if any authenticated user is allowed.</param>
public record AdminAccessRequirement(string? RequiredRole = null) : IAuthorizationRequirement;

/// <summary>
/// The requirement for the initial setup wizard.
/// </summary>
/// <remarks>
/// On a fresh installation there is neither a database nor an admin user, and the panel has to stay
/// reachable to create both. Only the few routes which are needed for that (the setup page and the
/// first-user creation) carry this requirement. It succeeds anonymously only while it is positively
/// confirmed that no admin user exists yet; afterwards it requires the administrator role, so the
/// rest of the panel (API, logs, management pages) is never reachable anonymously.
/// </remarks>
public sealed record AdminSetupRequirement : IAuthorizationRequirement;

/// <summary>
/// Handles the <see cref="AdminAccessRequirement"/>.
/// </summary>
public class AdminAccessRequirementHandler : AuthorizationHandler<AdminAccessRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminAccessRequirement requirement)
    {
        // Default-deny: an anonymous request never satisfies a role-based requirement, not even
        // during the initial setup window. The setup wizard itself uses <see cref="AdminSetupRequirement"/>
        // which has its own, narrowly scoped handler.
        if (context.User.Identity?.IsAuthenticated is not true)
        {
            return Task.CompletedTask;
        }

        if (requirement.RequiredRole is null || context.User.IsInRole(requirement.RequiredRole))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Handles the <see cref="AdminSetupRequirement"/>: anonymous access only while the panel has no
/// admin user yet; otherwise the administrator role is required.
/// </summary>
public class AdminSetupRequirementHandler : AuthorizationHandler<AdminSetupRequirement>
{
    private readonly AdminUserAvailabilityService _userAvailability;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminSetupRequirementHandler"/> class.
    /// </summary>
    /// <param name="userAvailability">The service which knows whether any user exists.</param>
    public AdminSetupRequirementHandler(AdminUserAvailabilityService userAvailability)
    {
        this._userAvailability = userAvailability;
    }

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminSetupRequirement requirement)
    {
        if (await this._userAvailability.IsConfirmedEmptyAsync().ConfigureAwait(false))
        {
            context.Succeed(requirement);
            return;
        }

        if (context.User.Identity?.IsAuthenticated is true
            && context.User.IsInRole(AdminRoles.Administrator))
        {
            context.Succeed(requirement);
        }
    }
}