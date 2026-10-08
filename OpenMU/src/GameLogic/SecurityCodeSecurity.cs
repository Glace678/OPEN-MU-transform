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
/// verification.
/// </summary>
public static class SecurityCodeSecurity
{
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
    /// On success, a legacy plaintext code is upgraded to a hash.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="providedCode">The code provided by the client.</param>
    /// <returns><c>true</c> if the code is correct; otherwise, <c>false</c>.</returns>
    public static bool VerifyCode(Account account, string providedCode)
    {
        var storedCode = account.SecurityCode;
        if (string.IsNullOrEmpty(storedCode) || string.IsNullOrEmpty(providedCode))
        {
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
            // Legacy plaintext code: compare in constant time, then upgrade the storage.
            valid = FixedTimeEquals(providedCode, storedCode);
            if (valid)
            {
                account.SecurityCode = HashCode(providedCode);
            }
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
