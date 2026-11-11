// <copyright file="GuildHostilityChangedArguments.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.ServerClients;

/// <summary>
/// Arguments for a guild hostility change event.
/// </summary>
public record GuildHostilityChangedArguments(uint GuildIdA, uint[] AllianceGuildIdsA, uint GuildIdB, uint[] AllianceGuildIdsB, bool Created);
