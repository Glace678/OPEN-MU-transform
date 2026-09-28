// <copyright file="ListenerAddressResolver.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network;

using System.Net;

/// <summary>
/// Resolves the local address to which game-facing TCP listeners bind.
/// </summary>
public static class ListenerAddressResolver
{
    /// <summary>
    /// The environment variable which defines the local listener address.
    /// </summary>
    public const string EnvironmentVariableName = "OPENMU_BIND_ADDRESS";

    /// <summary>
    /// Resolves the configured address. The legacy default remains all IPv4 interfaces.
    /// </summary>
    /// <returns>The configured local address.</returns>
    /// <exception cref="InvalidOperationException">The configured value is not an IPv4 address.</exception>
    public static IPAddress Resolve()
    {
        var configuredAddress = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(configuredAddress))
        {
            return IPAddress.Any;
        }

        if (!IPAddress.TryParse(configuredAddress, out var address)
            || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw new InvalidOperationException($"Environment variable {EnvironmentVariableName} must contain an IPv4 address, but was '{configuredAddress}'.");
        }

        return address;
    }
}
