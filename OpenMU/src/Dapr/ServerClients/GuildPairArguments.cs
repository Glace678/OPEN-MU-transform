// <copyright file="GuildPairArguments.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.ServerClients;

/// <summary>
/// Arguments identifying a pair of guilds.
/// </summary>
public record GuildPairArguments(uint GuildId1, uint GuildId2);
