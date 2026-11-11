// <copyright file="ForceSkillAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Skills;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The Force skill action.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ForceSkillAction_Name), Description = nameof(PlugInResources.ForceSkillAction_Description), ResourceType = typeof(PlugInResources))]
[Guid("552e4e3d-8215-44f4-bee3-b006da049eb2")]
public class ForceSkillAction : TargetedSkillDefaultPlugin
{
    /// <summary>
    /// The skill id of the force wave skill.
    /// </summary>
    protected const ushort ForceWaveSkillId = 66;
    private const ushort ForceWaveStrengSkillId = 509;

    private static readonly ConcurrentDictionary<AreaSkillSettings, FrustumBasedTargetFilter> FrustumFilters = new();

    /// <inheritdoc/>
    public override short Key => 60;

    /// <inheritdoc/>
    public override async ValueTask PerformSkillAsync(Player player, IAttackable target, ushort skillId)
    {
        // Special handling of force (wave) skill. The client might send skill id 60 (force),
        // even though it's performing force wave.
        if (skillId != ForceWaveStrengSkillId && player.SkillList?.ContainsSkill(ForceWaveSkillId) is true)
        {
            await base.PerformSkillAsync(player, target, ForceWaveSkillId).ConfigureAwait(false);
        }
        else
        {
            await base.PerformSkillAsync(player, target, skillId).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override IEnumerable<IAttackable> DetermineTargets(Player player, IAttackable targetedTarget, Skill skill)
    {
        if (skill.Number != ForceWaveSkillId && skill.Number != ForceWaveStrengSkillId)
        {
            return targetedTarget.GetAsEnumerable();
        }

        var targetsInRange = player.CurrentMap?
                    .GetAttackablesInRange(player.Position, skill.Range + 4)
                    .Where(a => a != player)
                    .Where(a => !a.IsAtSafezone() && a.IsActive())
                    .Where(a => player.GameContext.Configuration.AreaSkillHitsPlayer || a is not Player)
                    .Where(a => a.CheckSkillTargetRestrictions(player, skill))
                    .ToList()
            ?? [];

        if (skill.AreaSkillSettings is { UseFrustumFilter: true } areaSkillSettings)
        {
            var filter = FrustumFilters.GetOrAdd(
                areaSkillSettings,
                static s => new FrustumBasedTargetFilter(s.FrustumStartWidth, s.FrustumEndWidth, s.FrustumDistance, s.ProjectileCount > 0 ? s.ProjectileCount : 1));

            var rotationToTarget = (byte)((player.Position.GetAngleDegreeTo(targetedTarget.Position) / 360.0) * 256.0);
            targetsInRange = targetsInRange.Where(a => filter.IsTargetWithinBounds(player, a, rotationToTarget)).ToList();
        }
        else
        {
            targetsInRange = [];
        }

        if (!targetsInRange.Contains(targetedTarget))
        {
            targetsInRange.Add(targetedTarget);
        }

        return targetsInRange;
    }
}