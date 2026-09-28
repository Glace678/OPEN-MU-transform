// <copyright file="AccountCredentialSnapshot.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence;

/// <summary>Untracked credential values used to authorize an atomic replacement.</summary>
/// <param name="LoginName">The account name.</param>
/// <param name="PasswordHash">The current password hash.</param>
/// <param name="RecoveryCodeHash">The current recovery code hash, or null for an unenrolled account.</param>
public sealed record AccountCredentialSnapshot(string LoginName, string PasswordHash, string? RecoveryCodeHash);
