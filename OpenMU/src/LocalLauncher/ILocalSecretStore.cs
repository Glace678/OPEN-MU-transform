// <copyright file="ILocalSecretStore.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

/// <summary>Persists local credentials using the host's private storage policy.</summary>
public interface ILocalSecretStore
{
    /// <summary>Gets whether provisioning has completed.</summary>
    bool Exists { get; }

    /// <summary>Reads and validates the saved credentials.</summary>
    LocalSecrets Load();

    /// <summary>Atomically persists the credentials.</summary>
    void Save(LocalSecrets secrets);
}
