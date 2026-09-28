// <copyright file="DpapiSecretStore.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

using System.Security.Cryptography;
using System.Text.Json;

/// <summary>
/// Persists launcher secrets with Windows DPAPI and current-user scope.
/// </summary>
public sealed class DpapiSecretStore : ILocalSecretStore
{
    private static readonly byte[] AdditionalEntropy = Encoding.UTF8.GetBytes("OpenMU-Local.Secrets.v1");
    private readonly string _filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="DpapiSecretStore"/> class.
    /// </summary>
    /// <param name="filePath">The encrypted file path.</param>
    public DpapiSecretStore(string filePath)
    {
        this._filePath = filePath;
    }

    /// <summary>Gets a value indicating whether an encrypted secret file exists.</summary>
    public bool Exists => File.Exists(this._filePath);

    /// <summary>
    /// Creates a complete set of random database secrets and validates the administrator password.
    /// </summary>
    /// <param name="adminPanelPassword">The user-selected administrator password.</param>
    /// <returns>The generated local secrets.</returns>
    public static LocalSecrets Create(string adminPanelPassword)
    {
        if (string.IsNullOrWhiteSpace(adminPanelPassword) || adminPanelPassword.Length < 12)
        {
            throw new ArgumentException("后台管理员密码必须至少包含 12 个字符。", nameof(adminPanelPassword));
        }

        return new LocalSecrets
        {
            DatabaseAdminPassword = CreateRandomSecret(),
            ConfigurationPassword = CreateRandomSecret(),
            AccountPassword = CreateRandomSecret(),
            FriendPassword = CreateRandomSecret(),
            GuildPassword = CreateRandomSecret(),
            AdminPanelPassword = adminPanelPassword,
        };
    }

    /// <summary>
    /// Loads and decrypts the local secrets.
    /// </summary>
    /// <returns>The decrypted secrets.</returns>
    public LocalSecrets Load()
    {
        var protectedBytes = File.ReadAllBytes(this._filePath);
        var clearBytes = ProtectedData.Unprotect(protectedBytes, AdditionalEntropy, DataProtectionScope.CurrentUser);
        try
        {
            var secrets = JsonSerializer.Deserialize<LocalSecrets>(clearBytes)
                          ?? throw new InvalidDataException("本地密钥文件为空。");
            Validate(secrets);
            return secrets;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
        }
    }

    /// <summary>
    /// Encrypts and saves the local secrets atomically.
    /// </summary>
    /// <param name="secrets">The secrets to persist.</param>
    public void Save(LocalSecrets secrets)
    {
        Validate(secrets);
        Directory.CreateDirectory(Path.GetDirectoryName(this._filePath)!);
        var clearBytes = JsonSerializer.SerializeToUtf8Bytes(secrets);
        try
        {
            var protectedBytes = ProtectedData.Protect(clearBytes, AdditionalEntropy, DataProtectionScope.CurrentUser);
            var temporaryFile = this._filePath + ".tmp";
            File.WriteAllBytes(temporaryFile, protectedBytes);
            File.Move(temporaryFile, this._filePath, true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
        }
    }

    private static string CreateRandomSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    internal static void Validate(LocalSecrets secrets)
    {
        if (string.IsNullOrWhiteSpace(secrets.AdminPanelPassword)
            || secrets.AdminPanelPassword.Length < 12
            || new[]
            {
                secrets.DatabaseAdminPassword,
                secrets.ConfigurationPassword,
                secrets.AccountPassword,
                secrets.FriendPassword,
                secrets.GuildPassword,
            }.Any(secret => string.IsNullOrWhiteSpace(secret) || secret.Length < 32))
        {
            throw new InvalidDataException("本地密钥文件不完整或已损坏。");
        }
    }
}
