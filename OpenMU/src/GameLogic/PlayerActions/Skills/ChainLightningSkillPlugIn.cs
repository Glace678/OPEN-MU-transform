// <copyright file="ChainLightningSkillPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Skills;

using System.Runtime.InteropServices;

using MUnique.OpenMU.GameLogic.GuildWar;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Pathfinding;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Handles the chain lightning skill of the summoner class. Additionally to the attacked target, it will hit up to two additional targets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ChainLightningSkillPlugIn_Name), Description = nameof(PlugInResources.ChainLightningSkillPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("298C5FF8-03A2-476B-B064-A59E73DFCEB9")]
public class ChainLightningSkillPlugIn : IAreaSkillPlugIn
{
    /// <inheritdoc />
    public short Key => 215;

    /// <inheritdoc />
    public async ValueTask AfterTargetGotAttackedAsync(IAttacker attacker, IAttackable target, SkillEntry skillEntry, Point targetAreaCenter, HitInfo? hitInfo)
    {
        bool FilterTarget(IAttackable attackable)
        {
            if (!attackable.IsAlive || attackable.IsAtSafezone() || attackable == attacker)
            {
                return false;
            }

            if (attackable is Monster { SummonedBy: null } or Destructible)
            {
                return true;
            }

            if (attackable is Monster { SummonedBy: not null } summoned)
            {
                return FilterTarget(summoned.SummonedBy);
            }

            if (attackable is Player { DuelRoom.State: DuelState.DuelStarted } targetPlayer
                && attacker is Player { DuelRoom.State: DuelState.DuelStarted } duelPlayer
                && targetPlayer.DuelRoom == duelPlayer.DuelRoom
                && targetPlayer.DuelRoom.IsDuelist(targetPlayer) && targetPlayer.DuelRoom.IsDuelist(duelPlayer))
            {
                return true;
            }

            if (attackable is Player { GuildWarContext: { State: GuildWarState.Started } targetWarContext }
                && attacker is Player { GuildWarContext: { State: GuildWarState.Started } attackerWarContext }
                && targetWarContext.Score == attackerWarContext.Score
                && targetWarContext.Team != attackerWarContext.Team)
            {
                return true;
            }

            return false;
        }

        if (skillEntry.Skill is not { } skill)
        {
            return;
        }

        var secondTarget = target.CurrentMap?.GetAttackablesInRange(target.Position, 2).Where(FilterTarget).FirstOrDefault(t => t != target) ?? target;
        var thirdTarget = secondTarget.CurrentMap?.GetAttackablesInRange(secondTarget.Position, 2).Where(FilterTarget).FirstOrDefault(t => t != secondTarget && t != target) ?? secondTarget;

        var observable = attacker as IObservable;
        if (observable is null)
        {
            return;
        }

        await observable.ForEachWorldObserverAsync<IShowChainLightningPlugIn>(o => o.ShowLightningChainAnimationAsync(attacker, skill, [target, secondTarget, thirdTarget]), true).ConfigureAwait(false);

        var expectedMap = target.CurrentMap;
        static bool CanHitDeferredTarget(IAttacker currentAttacker, IAttackable deferredTarget, GameMap? map)
        {
            if (map is null || deferredTarget.CurrentMap != map || !deferredTarget.IsAlive || !deferredTarget.IsActive() || deferredTarget.IsAtSafezone())
            {
                return false;
            }

            if (currentAttacker is Player attackerPlayer)
            {
                return attackerPlayer.IsAlive && attackerPlayer.IsActive() && !attackerPlayer.IsAtSafezone() && attackerPlayer.CurrentMap == map;
            }

            return currentAttacker is not IAttackable attackableAttacker || (attackableAttacker.IsAlive && attackableAttacker.IsActive() && attackableAttacker.CurrentMap == map);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300).ConfigureAwait(false);

                // first attack 70 %
                if (CanHitDeferredTarget(attacker, secondTarget, expectedMap))
                {
                    var hit2Info = await secondTarget.AttackByAsync(attacker, skillEntry, false, 0.7).ConfigureAwait(false);
                    await secondTarget.TryApplyElementalEffectsAsync(attacker, skillEntry, hit2Info).ConfigureAwait(false);
                }

                await Task.Delay(300).ConfigureAwait(false);

                // second attack 50%
                if (CanHitDeferredTarget(attacker, thirdTarget, expectedMap))
                {
                    var hit3Info = await thirdTarget.AttackByAsync(attacker, skillEntry, false, 0.5).ConfigureAwait(false);
                    await thirdTarget.TryApplyElementalEffectsAsync(attacker, skillEntry, hit3Info).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                (attacker as Player)?.Logger.LogError(ex, "Unexpected error during Chain Lightning deferred hits.");
            }
        });
    }
}