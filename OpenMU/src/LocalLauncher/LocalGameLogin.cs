// <copyright file="LocalGameLogin.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

using System.Security.Cryptography;

/// <summary>Stable, installation-specific game credentials, separate from administrator credentials.</summary>
public sealed class LocalGameLogin
{
    private const string MobileLoginNamePurpose = "OpenMU-Mobile.LoginName.v1";
    private const string MobileLoginPasswordPurpose = "OpenMU-Mobile.LoginPassword.v1";

    private LocalGameLogin(string username, string password)
    {
        this.Username = username;
        this.Password = password;
    }

    /// <summary>Gets the ordinary game account name.</summary>
    public string Username { get; }

    /// <summary>Gets the game password, which must never be logged.</summary>
    public string Password { get; }

    /// <summary>Derives the same account after restart or a backup restore with the original keys.</summary>
    public static LocalGameLogin FromSecrets(LocalSecrets secrets)
    {
        DpapiSecretStore.Validate(secrets);
        var key = Encoding.UTF8.GetBytes(secrets.AccountPassword);
        try
        {
            var name = HMACSHA256.HashData(key, "OpenMU-Solo.LoginName.v1"u8);
            var password = HMACSHA256.HashData(key, "OpenMU-Solo.LoginPassword.v1"u8);
            return new LocalGameLogin(
                "solo" + Convert.ToHexString(name)[..6],
                Convert.ToBase64String(password)[..20].Replace('+', '-').Replace('/', '_'));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>Derives the mobile package's ordinary game credentials.</summary>
    /// <param name="packageKey">The 43-character base64url package key.</param>
    /// <returns>The stable game login for the package.</returns>
    public static LocalGameLogin FromMobilePackageKey(string packageKey)
    {
        if (!IsValidMobilePackageKey(packageKey))
        {
            throw new ArgumentException("The mobile package key must be 43 base64url characters.", nameof(packageKey));
        }

        var key = Encoding.UTF8.GetBytes(packageKey);
        try
        {
            var name = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(MobileLoginNamePurpose));
            var password = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(MobileLoginPasswordPurpose));
            return new LocalGameLogin(
                "mob" + Convert.ToHexString(name)[..7],
                Convert.ToBase64String(password).TrimEnd('=').Replace('+', '-').Replace('/', '_')[..20]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>Checks the syntax of an unpadded, 32-byte base64url package key.</summary>
    /// <param name="packageKey">The package key.</param>
    /// <returns><c>true</c> when the key has the required syntax; otherwise, <c>false</c>.</returns>
    internal static bool IsValidMobilePackageKey(string? packageKey) =>
        packageKey is { Length: 43 }
        && packageKey.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
