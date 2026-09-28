// <copyright file="AccountRecoveryCodeRequest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>Request body for issuing or replacing a recovery code after verifying the current password.</summary>
/// <param name="LoginName">The account name.</param>
/// <param name="CurrentPassword">The current game account password, not an administrator password.</param>
public sealed record AccountRecoveryCodeRequest(string? LoginName, string? CurrentPassword);
