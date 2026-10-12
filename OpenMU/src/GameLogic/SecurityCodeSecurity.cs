// <copyright file="SecurityCodeSecurity.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Security.Cryptography;
using System.Text;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Hashing and verification of the account security code used for character deletion
/// and guild kicks. New codes are stored as BCrypt hashes; codes still stored as legacy
/// plaintext are compared in constant time and upgraded to a hash on the first successful
/// verification. Failed attempts are throttled by the process-global <see cref="LoginThrottle"/>
/// (keyed by account login name): after <see cref="MaxFailedAttempts"/> failures, further
/// verifications are rejected until <see cref="LockoutDuration"/> has passed, and the counter
/// survives a client disconnect/reconnect within the same server process.
/// </summary>
public static class SecurityCodeSecurity
{
    /// <summary>
    /// The number of failed verification attempts after which the security code is temporarily locked.
    /// </summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>
    /// The duration for which verifications are rejected after reaching <see cref="MaxFailedAttempts"/>.
    /// </summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Hashes the specified security code for storage.
    /// </summary>
    /// <param name="securityCode">The security code.</param>
    /// <returns>The BCrypt hash, or an empty string for an empty code.</returns>
    public static string HashCode(string securityCode)
    {
        return string.IsNullOrEmpty(securityCode)
            ? string.Empty
            : BCrypt.Net.BCrypt.HashPassword(securityCode);
    }

    /// <summary>
    /// Verifies the provided code against the account's stored security code.
    /// On success, a legacy plaintext code is upgraded to a hash. Failed attempts are counted
    /// and may trigger a temporary lockout.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="providedCode">The code provided by the client.</param>
    /// <returns><c>true</c> if the code is correct; otherwise, <c>false</c>.</returns>
    public static bool VerifyCode(Account account, string providedCode)
    {
        var key = "sec:" + account.LoginName;
        var now = DateTime.UtcNow;
        if (LoginThrottle.IsLockedOut(key, now))
        {
            // Locked out: reject without burning a BCrypt verify on every guess.
            return false;
        }

        var storedCode = account.SecurityCode;
        if (string.IsNullOrEmpty(storedCode) || string.IsNullOrEmpty(providedCode))
        {
            // No code configured or nothing to verify: not a brute-force attempt, do not count it.
            return false;
        }

        bool valid;
        if (IsBcryptHash(storedCode))
        {
            try
            {
                valid = BCrypt.Net.BCrypt.Verify(providedCode, storedCode);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                valid = false;
            }
        }
        else
        {
            // Legacy plaintext code: compare in constant time, then upgrade the storage on success.
            valid = FixedTimeEquals(providedCode, storedCode);
            if (valid)
            {
                account.SecurityCode = HashCode(providedCode);
            }
        }

        if (valid)
        {
            LoginThrottle.Reset(key);
        }
        else
        {
            LoginThrottle.RegisterFailure(key, now, MaxFailedAttempts, LockoutDuration);
        }

        return valid;
    }

    private static bool IsBcryptHash(string value)
    {
        // BCrypt hashes look like $2a$, $2b$ or $2y$ followed by the cost factor.
        return value.Length >= 4
               && value[0] == '$'
               && value[1] == '2'
               && (value[2] == 'a' || value[2] == 'b' || value[2] == 'y')
               && value[3] == '$';
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
