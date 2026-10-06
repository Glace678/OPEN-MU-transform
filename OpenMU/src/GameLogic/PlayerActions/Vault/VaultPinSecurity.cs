// <copyright file="VaultPinSecurity.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Vault;

using System.Security.Cryptography;
using System.Text;
using MUnique.OpenMU.GameLogic;

/// <summary>
/// Encapsulates hashing, verification and brute-force throttling of vault pins.
/// </summary>
/// <remarks>
/// New pins are stored as BCrypt hashes. Pins which are still stored as legacy plaintext
/// (from before this change) are verified with a constant-time comparison and are upgraded
/// to a hash on the first successful unlock. After <see cref="MaxFailedAttempts"/> failed
/// attempts, further unlocks are rejected until <see cref="LockoutDuration"/> has passed.
/// </remarks>
public static class VaultPinSecurity
{
    /// <summary>
    /// The number of failed unlock attempts after which the vault is temporarily locked.
    /// </summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>
    /// The duration for which unlock attempts are rejected after reaching <see cref="MaxFailedAttempts"/>.
    /// </summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Hashes the specified pin for storage.
    /// </summary>
    /// <param name="pin">The pin.</param>
    /// <returns>The BCrypt hash of the pin.</returns>
    public static string HashPin(string pin) => BCrypt.Net.BCrypt.HashPassword(pin);

    /// <summary>
    /// Determines whether the vault is currently in the temporary lockout window.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="now">The current UTC time.</param>
    /// <returns><c>true</c> if unlock attempts are currently rejected; otherwise, <c>false</c>.</returns>
    public static bool IsLockedOut(Player player, DateTime now)
    {
        return player.VaultLockoutUntil is { } lockoutUntil && now < lockoutUntil;
    }

    /// <summary>
    /// Verifies the provided pin against the account's stored pin.
    /// On success, the failure counter is reset (and a legacy plaintext pin is upgraded to a hash).
    /// On failure, the failure counter is increased and a lockout may be started.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="pin">The pin provided by the client.</param>
    /// <param name="now">The current UTC time.</param>
    /// <returns><c>true</c> if the pin is correct; otherwise, <c>false</c>.</returns>
    public static bool VerifyPin(Player player, string pin, DateTime now)
    {
        var account = player.Account;
        var storedPin = account?.VaultPassword;
        if (account is null || string.IsNullOrEmpty(storedPin) || string.IsNullOrEmpty(pin))
        {
            return false;
        }

        bool valid;
        if (IsBcryptHash(storedPin))
        {
            valid = BCrypt.Net.BCrypt.Verify(pin, storedPin);
        }
        else
        {
            // Legacy plaintext pin: compare in constant time, then upgrade the storage on success.
            valid = FixedTimeEquals(pin, storedPin);
            if (valid)
            {
                account.VaultPassword = HashPin(pin);
            }
        }

        if (valid)
        {
            player.VaultUnlockFailCount = 0;
            player.VaultLockoutUntil = null;
        }
        else
        {
            RegisterFailure(player, now);
        }

        return valid;
    }

    private static void RegisterFailure(Player player, DateTime now)
    {
        player.VaultUnlockFailCount++;
        if (player.VaultUnlockFailCount >= MaxFailedAttempts)
        {
            player.VaultLockoutUntil = now.Add(LockoutDuration);
            player.VaultUnlockFailCount = 0;
        }
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
        if (providedBytes.Length != expectedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
