// <copyright file="RageSkillAttackAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Skills;

using System.Collections.Concurrent;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Action for rage attacks.
/// </summary>
public class RageSkillAttackAction
{
    private const int MaximumTargetsPerAttack = 5;

    private const int HitsPerTarget = 2;

    private const ushort DarkSideSkill = 263;
    private const ushort DarkSideStrengthenerSkill = 559;
    private static readonly ConcurrentDictionary<Player, byte> ActiveAttacks = new();

    /// <summary>
    /// Attacks the target with a rage skill.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="explicitTargetId">The explicit target identifier.</param>
    /// <param name="skillId">The skill identifier.</param>
    public async ValueTask AttackAsync(Player player, ushort explicitTargetId, ushort skillId)
    {
        if (!player.IsAlive || player.CurrentMap is not { } map || player.IsAtSafezone()
            || skillId is not (DarkSideSkill or DarkSideStrengthenerSkill)
            || player.SkillList?.GetSkill(skillId) is not { Skill: { } definition } skill
            || !ActiveAttacks.TryAdd(player, 0))
        {
            return;
        }

        var scheduled = false;
        try
        {
            scheduled = await this.StartAttackAsync(player, map, explicitTargetId, skill, definition).ConfigureAwait(false);
        }
        finally
        {
            if (!scheduled)
            {
                ActiveAttacks.TryRemove(player, out _);
            }
        }
    }

    private async ValueTask<bool> StartAttackAsync(Player player, GameMap map, ushort explicitTargetId, SkillEntry skill, Skill definition)
    {
        var explicitTarget = player.GetObject(explicitTargetId) as IAttackable;
        if (explicitTarget is not null && (!explicitTarget.IsInRange(player.Position, definition.Range)
            || !explicitTarget.CheckSkillTargetRestrictions(player, definition)))
        {
            return false;
        }

        var targets = new List<IAttackable>(MaximumTargetsPerAttack);
        if (explicitTarget is not null)
        {
            targets.Add(explicitTarget);
        }

        targets.AddRange(
            map.GetAttackablesInRange(player.Position, definition.Range)
                .Where(t => t != explicitTarget && t is not Player && t.IsAlive && t.CheckSkillTargetRestrictions(player, definition))
                .OrderBy(t => t.GetDistanceTo(player))
                .Take(MaximumTargetsPerAttack - targets.Count));

        if (targets.Count == 0)
        {
            return false;
        }

        if (player.GameContext.PlugInManager.GetPlugInPoint<ISpeedHackCheatCheckPlugIn>() is { } speedCheck)
        {
            var eventArgs = new SpeedHackCheckEventArgs();
            await speedCheck.AttackCheatCheckAsync(player, eventArgs).ConfigureAwait(false);
            if (eventArgs.IsCheatDetected)
            {
                return false;
            }
        }

        if (!await player.TryConsumeForSkillAsync(skill).ConfigureAwait(false))
        {
            return false;
        }

        var skillId = (ushort)definition.Number;
        await player.InvokeViewPlugInAsync<IShowRageAttackRangePlugIn>(p => p.ShowRageAttackRangeAsync(skillId, targets)).ConfigureAwait(false);
        if (explicitTarget is null)
        {
            await player.ForEachWorldObserverAsync<IShowRageAttackPlugIn>(p => p.ShowAttackAsync(player, targets.First(), skillId), true).ConfigureAwait(false);
        }

        bool isCombo = false;
        if (player.ComboState is { } comboState)
        {
            isCombo = await comboState.RegisterSkillAsync(definition).ConfigureAwait(false);
            if (isCombo)
            {
                await player.ForEachWorldObserverAsync<IShowSkillAnimationPlugIn>(p => p.ShowComboAnimationAsync(player, explicitTarget), true).ConfigureAwait(false);
            }
        }

        _ = this.RunAttacksAsync(player, map, skill, definition, targets, isCombo);
        return true;
    }

    private async ValueTask RunAttacksAsync(Player player, GameMap map, SkillEntry skill, Skill definition, List<IAttackable> targets, bool isCombo)
    {
        try
        {
            for (int i = 0; i < HitsPerTarget; i++)
            {
                await Task.Delay(200).ConfigureAwait(false);
                if (!player.IsAlive || player.CurrentMap != map || player.IsAtSafezone())
                {
                    return;
                }

                foreach (var target in targets)
                {
                    if (target.IsAlive && target.CurrentMap == map && target.IsInRange(player.Position, definition.Range)
                        && target.CheckSkillTargetRestrictions(player, definition))
                    {
                        await target.AttackByAsync(player, skill, isCombo, 1.0 / HitsPerTarget, i == HitsPerTarget - 1).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Error running rage attack.");
        }
        finally
        {
            ActiveAttacks.TryRemove(player, out _);
        }
    }
}
