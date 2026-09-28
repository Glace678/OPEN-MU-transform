// <copyright file="PasswordResetRequest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>
/// Request body for <c>POST /api/registration/reset-password</c>.
/// </summary>
/// <remarks>
/// Public recovery requires possession of the independently generated recovery code.
/// Omitting the code never permits a public reset. Legacy local maintenance resets
/// remain disabled by default and reject proxy headers even when explicitly enabled.
/// </remarks>
/// <param name="LoginName">The account login name.</param>
/// <param name="NewPassword">The desired new password (3-20 characters, no spaces).</param>
/// <param name="ConfirmNewPassword">The repeated new password; must match <paramref name="NewPassword"/>.</param>
/// <param name="RecoveryCode">The one-time recovery code. Older clients may omit it but cannot recover remotely.</param>
public sealed record PasswordResetRequest(string? LoginName, string? NewPassword, string? ConfirmNewPassword, string? RecoveryCode = null);
