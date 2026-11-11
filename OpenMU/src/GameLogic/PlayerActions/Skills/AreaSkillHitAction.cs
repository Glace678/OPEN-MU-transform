// <copyright file="AreaSkillHitAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Skills;

/// <summary>
/// Action to hit targets with an area skill, which requires explicit hits <seealso cref="SkillType.AreaSkillExplicitHits"/>.
/// </summary>
public class AreaSkillHitAction
{
    /// <summary>
    /// Attacks the target by the player with the specified skill.
    /// </summary>
    /// <param name="player">The player who is performing the skill.</param>
    /// <param name="target">The target.</param>
    /// <param name="skill">The skill.</param>
    public async ValueTask AttackTargetAsync(Player player, IAttackable target, SkillEntry skill)
    {
        if (!player.IsAlive
            || skill.Skill?.SkillType != SkillType.AreaSkillExplicitHits
            || !target.IsAlive
            || target.IsAtSafezone()
            || player.CurrentMap is null
            || player.CurrentMap != target.CurrentMap
            || (target is Player && !player.GameContext.Configuration.AreaSkillHitsPlayer))
        {
            return;
        }

        if (player.IsAtSafezone())
        {
            // It's possible, when the player did some area skill (Evil Spirit), and walked into the safezone.
            // We don't log it as hacker attempt, since the AreaSkillAttackAction already does handle this.
            return;
        }

        var maxRange = Math.Max(
            (int)skill.Skill.Range,
            Math.Max(
                skill.Skill.AreaSkillSettings?.EffectRange ?? 0,
                (int)Math.Ceiling(skill.Skill.AreaSkillSettings?.FrustumDistance ?? 0))) + 4;
        if (!player.IsInRange(target.Position, maxRange))
        {
            return;
        }

        if (target.CheckSkillTargetRestrictions(player, skill.Skill))
        {
            var hitInfo = await target.AttackByAsync(player, skill, false).ConfigureAwait(false);
            await target.TryApplyElementalEffectsAsync(player, skill, hitInfo).ConfigureAwait(false);
        }
    }
}