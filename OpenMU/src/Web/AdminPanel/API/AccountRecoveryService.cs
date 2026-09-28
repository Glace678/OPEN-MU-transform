// <copyright file="AccountRecoveryService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

using System.Security.Cryptography;
using System.Text;
using MUnique.OpenMU.Persistence;

/// <summary>Creates independent high-entropy recovery credentials and consumes them atomically.</summary>
public static class AccountRecoveryService
{
    /// <summary>Generates a new recovery code. The returned value must not be logged or stored as plaintext.</summary>
    /// <returns>A grouped hexadecimal code derived from 32 random bytes.</returns>
    public static string GenerateCode()
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return string.Join("-", code.Chunk(8).Select(group => new string(group)));
    }

    /// <summary>Validates and hashes a recovery code, accepting the displayed grouping and lowercase hex.</summary>
    /// <param name="code">The recovery credential.</param>
    /// <param name="hash">The canonical SHA-256 hash, or an empty value for invalid input.</param>
    /// <returns>True when the code has the required format.</returns>
    public static bool TryHashCode(string? code, out string hash)
    {
        hash = string.Empty;
        if (code is null || code.Length > 80)
        {
            return false;
        }

        var canonical = code.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (canonical.Length != 64 || canonical.Any(character => character is not (>= '0' and <= '9') and not (>= 'A' and <= 'F')))
        {
            return false;
        }

        hash = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(canonical)));
        return true;
    }

    /// <summary>Resets a password after proving recovery-code ownership; a successful result replaces the old code.</summary>
    /// <param name="context">Storage which supports atomic credential replacement.</param>
    /// <param name="loginName">The account name.</param>
    /// <param name="recoveryCode">The owned recovery credential.</param>
    /// <param name="newPassword">The validated new password.</param>
    /// <returns>The replacement recovery code only after a successful atomic update, otherwise null.</returns>
    public static async Task<string?> ResetAsync(IAccountCredentialContext context, string loginName, string recoveryCode, string newPassword)
    {
        if (!TryHashCode(recoveryCode, out var suppliedHash))
        {
            return null;
        }

        var expected = await context.ReadCredentialsAsync(loginName).ConfigureAwait(false);
        if (expected is null || !HashesMatch(suppliedHash, expected.RecoveryCodeHash))
        {
            return null;
        }

        var replacement = GenerateCode();
        _ = TryHashCode(replacement, out var newRecoveryHash);
        var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        return await context.TryReplaceCredentialsAsync(expected, newPasswordHash, newRecoveryHash).ConfigureAwait(false)
            ? replacement
            : null;
    }

    /// <summary>Enrolls an existing account or replaces a lost recovery code only after verifying its current password.</summary>
    /// <param name="context">Storage which supports atomic credential replacement.</param>
    /// <param name="loginName">The account name.</param>
    /// <param name="currentPassword">The current game account password.</param>
    /// <returns>The replacement recovery code after a successful update, otherwise null.</returns>
    public static async Task<string?> IssueAsync(IAccountCredentialContext context, string loginName, string currentPassword)
    {
        var expected = await context.ReadCredentialsAsync(loginName).ConfigureAwait(false);
        if (expected is null || !BCrypt.Net.BCrypt.Verify(currentPassword, expected.PasswordHash))
        {
            return null;
        }

        var replacement = GenerateCode();
        _ = TryHashCode(replacement, out var newRecoveryHash);
        return await context.TryReplaceCredentialsAsync(expected, expected.PasswordHash, newRecoveryHash).ConfigureAwait(false)
            ? replacement
            : null;
    }

    private static bool HashesMatch(string suppliedHash, string? storedHash)
    {
        if (storedHash is not { Length: 64 } || storedHash.Any(character => character is not (>= '0' and <= '9') and not (>= 'A' and <= 'F')))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(suppliedHash), Convert.FromHexString(storedHash));
    }
}
