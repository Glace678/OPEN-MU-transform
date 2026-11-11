// <copyright file="GuildWarKillScorePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.GuildWar;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// This plugin increases the score of the soccer result, if a kill occurred.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.GuildWarKillScorePlugIn_Name), Description = nameof(PlugInResources.GuildWarKillScorePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("2D4E16CD-B7FF-4ED3-B4B1-4AABD04BAD71")]
public class GuildWarKillScorePlugIn : IAttackableGotKilledPlugIn
{
    /// <summary>
    /// Is called when an <see cref="IAttackable" /> object got killed by another.
    /// </summary>
    /// <param name="killed">The killed <see cref="IAttackable" />.</param>
    /// <param name="killer">The killer.</param>
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (killer is Player { GuildWarContext: { State: GuildWarState.Started } killerWarContext } killerPlayer
            && killed is Player { GuildWarContext: { State: GuildWarState.Started } killedWarContext } killedPlayer
            && killerWarContext.Score == killedWarContext.Score
            && killerWarContext.Team != killedWarContext.Team)
        {
            var score = killedPlayer.GuildStatus?.Position == GuildPosition.GuildMaster ? (byte)2 : (byte)1;
            if (killerWarContext.Team == GuildWarTeam.First)
            {
                killerWarContext.Score.IncreaseFirstGuildScore(score);
            }
            else
            {
                killerWarContext.Score.IncreaseSecondGuildScore(score);
            }
        }
    }
}