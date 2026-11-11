// <copyright file="GuildHostilityArguments.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.ServerClients;

/// <summary>
/// Arguments for setting or clearing guild hostility.
/// </summary>
public record GuildHostilityArguments(uint GuildIdA, uint GuildIdB, bool Created);
