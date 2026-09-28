// <copyright file="AccountRegistrationRequest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>
/// Request body for <c>POST /api/registration/create</c>.
/// </summary>
/// <param name="LoginName">The desired login name (3-10 ASCII letters or digits).</param>
/// <param name="Password">The desired password (3-20 characters).</param>
/// <param name="ConfirmPassword">The repeated password; must match <paramref name="Password"/>.</param>
/// <param name="SecurityCode">The code used for character deletion and guild actions. Older clients may omit it;
/// the game keeps using the account password as its legacy fallback in that case.</param>
public sealed record AccountRegistrationRequest(
    string? LoginName,
    string? Password,
    string? ConfirmPassword,
    string? SecurityCode = null);
