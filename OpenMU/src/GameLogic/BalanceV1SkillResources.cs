// <copyright file="BalanceV1SkillResources.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>Opt-in AP budgets for reviewed, learned Season 6 routine attacks only.</summary>
public static class BalanceV1SkillResources
{
    private const double ReserveHorizonSeconds = 120;

    /// <summary>Identifies the four reviewed routine skill IDs, not a blanket physical/area category.</summary>
    /// <param name="number">The live skill number.</param>
    /// <returns>Whether the base skill has an explicitly reviewed resource contract.</returns>
    public static bool IsReviewedRoutineSkill(short number) => number is 43 or 52 or 55 or 262;

    /// <summary>Gets a routine AP budget without granting a skill or changing its MP requirement.</summary>
    /// <param name="attacker">The resource owner.</param>
    /// <param name="requirement">The original consume requirement.</param>
    /// <param name="entry">The actual learned skill entry.</param>
    /// <param name="cost">The positive base AP cost before existing reductions.</param>
    /// <returns>Whether this exact live contract has an opt-in resource rule.</returns>
    public static bool TryGetAbilityCost(IAttacker attacker, AttributeRequirement requirement, SkillEntry? entry, out int cost)
    {
        cost = 0;
        if (requirement.Attribute != Stats.CurrentAbility
            || attacker is not Player { Attributes: { } attributes, SelectedCharacter.CharacterClass: { } characterClass } player
            || !BalanceV1.IsEnabled(player.GameContext.Configuration)
            || SoloBalance.IsEnabled(player.GameContext.Configuration)
            || entry?.Skill is not { MasterDefinition: null, DamageType: DamageType.Physical } skill
            || entry.Level != 0
            || !ReferenceEquals(player.SkillList?.GetSkill((ushort)skill.Number), entry)
            || !player.GameContext.Configuration.Skills.Contains(skill)
            || !skill.QualifiedCharacters.Contains(characterClass)
            || !skill.ConsumeRequirements.Contains(requirement)
            || skill.Requirements.Any(value => attributes[value.Attribute] < value.MinimumValue))
        {
            return false;
        }

        // Explicit IDs/types/costs avoid treating burst, master, buff or custom skills as cheap aliases.
        var contract = skill.Number switch
        {
            43 when characterClass.Number is 6 or 7 => (SkillType.DirectHit, Hits: 1, Mana: 15, Ability: 12),
            52 when characterClass.Number is 8 or 10 or 11 => (SkillType.AreaSkillAutomaticHits, Hits: 1, Mana: 7, Ability: 9),
            55 when characterClass.Number is 12 or 13 => (SkillType.AreaSkillAutomaticHits, Hits: 1, Mana: 15, Ability: 20),
            262 when characterClass.Number is 24 or 25 => (SkillType.DirectHit, Hits: 4, Mana: 15, Ability: 20),
            _ => default,
        };
        if (contract.Ability == 0 || skill.SkillType != contract.Item1 || skill.NumberOfHitsPerAttack != contract.Hits
            || requirement.MinimumValue != contract.Ability
            || skill.ConsumeRequirements.Count != 2
            || skill.ConsumeRequirements.Count(value => value.Attribute == Stats.CurrentAbility) != 1
            || skill.ConsumeRequirements.Count(value => value.Attribute == Stats.CurrentMana && value.MinimumValue == contract.Mana) != 1)
        {
            return false;
        }

        var maximum = attributes[Stats.MaximumAbility];
        var recovery = ((maximum * attributes[Stats.AbilityRecoveryMultiplier]) + attributes[Stats.AbilityRecoveryAbsolute])
            / Stats.AbilityRegeneration.Interval.TotalSeconds;
        if (!float.IsFinite(maximum) || maximum <= 0 || !double.IsFinite(recovery) || recovery < 0)
        {
            return false;
        }

        // At one cast/second the net drain is at most half a full AP pool per minute.
        var budget = Math.Max(1, Math.Floor(recovery + (maximum / ReserveHorizonSeconds)));
        cost = (int)Math.Min(contract.Ability, budget);
        return true;
    }
}
