// <copyright file="UnixSecretStore.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

using System.Security.Cryptography;
using System.Text.Json;

/// <summary>
/// Stores Unix desktop credentials in an owner-only file inside an owner-only directory.
/// This is filesystem protection, not DPAPI or encryption at rest.
/// </summary>
public sealed class UnixSecretStore : ILocalSecretStore
{
    private readonly string _path;

    /// <summary>Creates a store at an explicit private path.</summary>
    public UnixSecretStore(string path)
    {
        this._path = Path.GetFullPath(path);
    }

    /// <inheritdoc />
    public bool Exists => File.Exists(this._path);

    /// <inheritdoc />
    public LocalSecrets Load()
    {
        LocalPlatform.RequireUnixDesktop();
        LocalPlatform.RejectLink(this._path);
        if ((File.GetUnixFileMode(this._path) & ~LocalPlatform.PrivateFileMode) != 0)
        {
            throw new InvalidDataException("Local credential file must have owner-only permissions (0600).");
        }

        var bytes = File.ReadAllBytes(this._path);
        try
        {
            var secrets = JsonSerializer.Deserialize<LocalSecrets>(bytes)
                ?? throw new InvalidDataException("Local credentials are empty.");
            DpapiSecretStore.Validate(secrets);
            return secrets;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <inheritdoc />
    public void Save(LocalSecrets secrets)
    {
        LocalPlatform.RequireUnixDesktop();
        DpapiSecretStore.Validate(secrets);
        LocalPlatform.CreatePrivateDirectory(Path.GetDirectoryName(this._path)!);
        LocalPlatform.RejectLink(this._path);
        var temporary = this._path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(secrets);
        try
        {
            using (var output = new FileStream(temporary, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                UnixCreateMode = LocalPlatform.PrivateFileMode,
            }))
            {
                output.Write(bytes);
                output.Flush(flushToDisk: true);
            }

            File.Move(temporary, this._path, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            File.Delete(temporary);
        }
    }
}
