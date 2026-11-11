// <copyright file="UpdateDuelScorePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Duel;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// This plugin increases the score of the duel result, when a kill occurred.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.UpdateDuelScorePlugIn_Name), Description = nameof(PlugInResources.UpdateDuelScorePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("C5CAC184-F8A9-41A0-A34E-04A07DB81F5E")]
public class UpdateDuelScorePlugIn : IAttackableGotKilledPlugIn
{
    /// <summary>
    /// Is called when an <see cref="IAttackable" /> object got killed by another.
    /// </summary>
    /// <param name="killed">The killed <see cref="IAttackable" />.</param>
    /// <param name="killer">The killer.</param>
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        var killerPlayer = killer as Player ?? (killer as IPlayerSurrogate)?.Owner;
        if (killerPlayer is { DuelRoom: { } duelRoom }
            && killed is Player { DuelRoom: not null } killedPlayer
            && duelRoom == killedPlayer.DuelRoom
            && killerPlayer != killedPlayer
            && duelRoom.IsDuelist(killerPlayer)
            && duelRoom.IsDuelist(killedPlayer))
        {
            using var l = await duelRoom.Lock.LockAsync();
            if (duelRoom.State is not DuelState.DuelStarted)
            {
                return;
            }

            if (duelRoom.Requester == killerPlayer)
            {
                duelRoom.ScoreRequester++;
            }
            else if (duelRoom.Opponent == killerPlayer)
            {
                duelRoom.ScoreOpponent++;
            }
        }
    }
}