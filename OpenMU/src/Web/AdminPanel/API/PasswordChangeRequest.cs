// <copyright file="PasswordChangeRequest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>
/// Request body for <c>POST /api/registration/change-password</c>.
/// </summary>
/// <param name="LoginName">The account login name.</param>
/// <param name="OldPassword">The current password; proves account ownership.</param>
/// <param name="NewPassword">The desired new password (3-20 characters, no spaces).</param>
/// <param name="ConfirmNewPassword">The repeated new password; must match <paramref name="NewPassword"/>.</param>
public sealed record PasswordChangeRequest(string? LoginName, string? OldPassword, string? NewPassword, string? ConfirmNewPassword);
