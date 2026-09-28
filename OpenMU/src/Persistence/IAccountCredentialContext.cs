// <copyright file="IAccountCredentialContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence;

using System.Threading;

/// <summary>
/// Storage capability for credential reads and atomic compare-and-swap updates.
/// A recovery code must never be consumed using an unconditional account save.
/// </summary>
public interface IAccountCredentialContext
{
    /// <summary>Reads only the current persisted credentials without using the account cache.</summary>
    /// <param name="loginName">The account name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current credentials, or null if the account is absent.</returns>
    ValueTask<AccountCredentialSnapshot?> ReadCredentialsAsync(string loginName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically replaces both hashes only when both persisted old hashes still match.
    /// This operation must not reload and retry with a different expected snapshot.
    /// </summary>
    /// <param name="expected">The exact credential snapshot which authorized the operation.</param>
    /// <param name="newPasswordHash">The password hash to persist.</param>
    /// <param name="newRecoveryCodeHash">The recovery code hash to persist.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True only when exactly one account was updated.</returns>
    ValueTask<bool> TryReplaceCredentialsAsync(
        AccountCredentialSnapshot expected,
        string newPasswordHash,
        string? newRecoveryCodeHash,
        CancellationToken cancellationToken = default);
}
