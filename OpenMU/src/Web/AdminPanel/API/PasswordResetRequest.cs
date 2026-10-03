// <copyright file="PasswordResetRequest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>
/// Request body for <c>POST /api/registration/reset-password</c>.
/// </summary>
/// <remarks>
/// Public recovery requires possession of the independently generated recovery code.
/// Omitting the code never permits a public reset: the legacy local maintenance path
/// stays disabled by default and, when enabled, requires the maintenance token from the
/// server's <c>Data/Keys/maintenance-token.txt</c> file instead of trusting the caller's
/// network position (loopback or proxy headers cannot prove account ownership).
/// </remarks>
/// <param name="LoginName">The account login name.</param>
/// <param name="NewPassword">The desired new password (8-20 characters, no spaces).</param>
/// <param name="ConfirmNewPassword">The repeated new password; must match <paramref name="NewPassword"/>.</param>
/// <param name="RecoveryCode">The one-time recovery code. Older clients may omit it but cannot recover remotely.</param>
/// <param name="MaintenanceToken">
/// The maintenance token required for a reset without a recovery code; must match the
/// contents of <c>Data/Keys/maintenance-token.txt</c> on the server.
/// </param>
public sealed record PasswordResetRequest(
    string? LoginName,
    string? NewPassword,
    string? ConfirmNewPassword,
    string? RecoveryCode = null,
    string? MaintenanceToken = null);
