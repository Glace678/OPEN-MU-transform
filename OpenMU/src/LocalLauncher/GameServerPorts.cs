// <copyright file="GameServerPorts.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

/// <summary>
/// Canonical TCP ports of the OpenMU game services. These are the values shipped in the
/// server configuration; every consumer (port pre-checks, collision validation) reads
/// them from here so a change is made in exactly one place.
/// </summary>
public static class GameServerPorts
{
    /// <summary>Gets the default ports and the component that listens on each.</summary>
    public static IReadOnlyList<(int Port, string Component)> Defaults { get; } = new[]
    {
        (44405, "1.04d 连接服务器"),
        (44406, "2.04d 连接服务器"),
        (55901, "1.04d 游戏服务器"),
        (55902, "2.04d 游戏服务器"),
        (55980, "聊天服务器"),
    };

    /// <summary>Gets just the port numbers of the default game services.</summary>
    public static IReadOnlyList<int> Ports { get; } = Defaults.Select(entry => entry.Port).ToArray();
}
