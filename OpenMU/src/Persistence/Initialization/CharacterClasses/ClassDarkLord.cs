// <copyright file="ClassDarkLord.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.CharacterClasses;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Initialization of character classes data.
/// </summary>
internal partial class CharacterClassInitialization
{
    private CharacterClass CreateDarkLord(CharacterClassNumber number, string name, bool isMaster, CharacterClass? nextGenerationClass, bool canGetCreated)
    {
        var energyMinus15 = this.Context.CreateNew<AttributeDefinition>(Guid.NewGuid(), "TotalEnergy minus 15", "TotalEnergy minus 15");
        this.GameConfiguration.Attributes.Add(energyMinus15);

        var result = this.Context.CreateNew<CharacterClass>();
        result.SetGuid((byte)number);
        this.GameConfiguration.CharacterClasses.Add(result);
        result.CanGetCreated = canGetCreated;
        result.CreationAllowedFlag = 2;
        result.HomeMap = this.GameConfiguration.Maps.FirstOrDefault(map => map.Number == LorenciaMapId);
        result.Number = (byte)number;
        result.Name = name;
        result.LevelRequirementByCreation = 250;
        result.IsMasterClass = isMaster;
        result.NextGenerationClass = nextGenerationClass;
        result.LevelWarpRequirementReductionPercent = (int)Math.Ceiling(100.0 / 3);
        result.StatAttributes.Add(this.CreateStatAttributeDefinition(Stats.Level, 1, false));
        result.StatAttributes.Add(this.CreateStatAttributeDefinition(Stats.PointsPerLevelUp, 5, false));
        result.StatAttributes.Add(this.CreateStatAttributeDefinition(Stats.BaseStrength, 26, true));
        result.StatAttributes.Add(this.CreateStatAttributeDefinition(Stats.BaseAgility, 20, true));
        result.StatAttributes.Add(this.CreateStatAttributeDefinition(Stats.BaseVitality, 20, true));
        result.StatAttributes.Add(this.CreateStatAttributeDefinition(Stats.BaseEnergy, 15, true));
        result.StatAttributes.Add(this.CreateStatAttributeDefinition(Stats.BaseLeadership, 25, true));
        result.StatAttributes.Add(this.CreateStatAttributeDefinition(Stats.CurrentHealth, 90, false));
        result.StatAttributes.Add(this.CreateStatAttributeDefinition(Stats.CurrentMana, 40, false));
        result.StatAttributes.Add(this.CreateStatAttributeDefinition(Stats.CurrentAbility, 1, false));
        result.StatAttributes.Add(this.CreateStatAttributeDefinition(Stats.IsInSafezone, 1, false));
        result.StatAttributes.Add(this.CreateStatAttributeDefinition(Stats.IsResting, 0, false));
        result.StatAttributes.Add(this.CreateStatAttributeDefinition(Stats.Resets, 0, false));
        result.StatAttributes.Add(this.CreateStatAttributeDefinition(Stats.NearbyPartyMemberCount, 0, false));

        this.AddCommonAttributeRelationships(result.AttributeCombinations);

        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.TotalLeadership, 1, Stats.BaseLeadership));

        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.DefenseBase, 0.1438f, Stats.TotalAgility));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.DefenseRatePvm, 0.1432f, Stats.TotalAgility));

        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.AttackRatePvm, 5, Stats.TotalLevel));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.AttackRatePvm, 2.5f, Stats.TotalAgility));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.AttackRatePvm, 0.1667f, Stats.TotalStrength));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.AttackRatePvm, 0.0985f, Stats.TotalLeadership));

        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.AttackSpeed, 0.0965f, Stats.TotalAgility));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MagicSpeed, 0.0965f, Stats.TotalAgility));

        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumAbility, 0.15f, Stats.TotalEnergy));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumAbility, 0.1f, Stats.TotalVitality));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumAbility, 0.2f, Stats.TotalAgility));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumAbility, 0.3f, Stats.TotalStrength));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumAbility, 0.3f, Stats.TotalLeadership));

        result.AttributeCombinations.Add(this.CreateAttributeRelationship(energyMinus15, -15, Stats.TotalEnergy, InputOperator.Add));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumMana, 1.5f, energyMinus15));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumMana, 1, Stats.TotalLevel));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumHealth, 1.5f, Stats.TotalLevel));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumHealth, 2, Stats.TotalVitality));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MinimumPhysBaseDmg, 0.1432f, Stats.TotalStrength));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumPhysBaseDmg, 0.1985f, Stats.TotalStrength));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MinimumPhysBaseDmg, 0.0716f, Stats.TotalEnergy));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumPhysBaseDmg, 0.0985f, Stats.TotalEnergy));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.SkillMultiplier, 0.0005f, Stats.TotalEnergy));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.RavenAttackDamageIncrease, 0.0102f, Stats.ScepterRise));

        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumGuildSize, 0.1f, Stats.TotalLeadership));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.DamageReceiveDecrement, 1, Stats.DamageReceiveHorseDecrement, InputOperator.Add, AggregateType.Multiplicate));

        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.FenrirBaseDmg, 0.1985f, Stats.TotalStrength));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.FenrirBaseDmg, 0.1985f, Stats.TotalAgility));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.FenrirBaseDmg, 0.1428f, Stats.TotalVitality));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.FenrirBaseDmg, 0.3334f, Stats.TotalEnergy));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.FenrirBaseDmg, 0.3334f, Stats.TotalLeadership));

        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.RavenMinimumDamage, 0.1245f, Stats.TotalLeadership));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.RavenMinimumDamage, 15, Stats.RavenLevel));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.RavenMaximumDamage, 0.2485f, Stats.TotalLeadership));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.RavenMaximumDamage, 15, Stats.RavenLevel));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.RavenAttackSpeed, 0.0225f, Stats.TotalLeadership));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.RavenAttackSpeed, 0.8125f, Stats.RavenLevel));
        result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.RavenAttackRate, 1.0667f, Stats.RavenLevel));

        result.AttributeCombinations.Add(this.CreateConditionalRelationship(Stats.RavenBonusDamage, Stats.IsScepterEquipped, Stats.ScepterPetBonusDamage));
        result.AttributeCombinations.Add(this.CreateConditionalRelationship(Stats.BonusDamageWithScepter, Stats.BonusDamageWithScepterCmdDiv, Stats.TotalLeadership));
        result.AttributeCombinations.Add(this.CreateConditionalRelationship(Stats.MasterSkillPhysBonusDmg, Stats.IsScepterEquipped, Stats.BonusDamageWithScepter));
        result.AttributeCombinations.Add(this.CreateConditionalRelationship(Stats.DefenseFinal, Stats.IsHorseEquipped, Stats.BonusDefenseWithHorse, aggregateType: AggregateType.AddFinal));

        if (!this.UseClassicPvp)
        {
            result.StatAttributes.Add(this.CreateStatAttributeDefinition(Stats.CurrentShield, 1, false));

            result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumShield, 1.2f, Stats.TotalEnergy));
            result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumShield, 1.2f, Stats.TotalVitality));
            result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumShield, 1.2f, Stats.TotalAgility));
            result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumShield, 1.2f, Stats.TotalStrength));
            result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumShield, 1.2f, Stats.TotalLeadership));
            result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumShield, 1f, Stats.DefenseFinal));
            result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumShieldTemp, 2f, Stats.TotalLevel, InputOperator.Exponentiate));
            result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.MaximumShield, 1f / 30f, Stats.MaximumShieldTemp));

            result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.DefenseRatePvp, 0.5f, Stats.TotalAgility));
            result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.DefenseRatePvp, 2, Stats.TotalLevel));

            result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.AttackRatePvp, 4.0f, Stats.TotalAgility));
            result.AttributeCombinations.Add(this.CreateAttributeRelationship(Stats.AttackRatePvp, 3, Stats.TotalLevel));
        }

        result.BaseAttributeValues.Add(this.CreateConstValueAttribute(38, Stats.MaximumMana));
        result.BaseAttributeValues.Add(this.CreateConstValueAttribute(48.5f, Stats.MaximumHealth));
        result.BaseAttributeValues.Add(this.CreateConstValueAttribute(2, Stats.SkillMultiplier));
        result.BaseAttributeValues.Add(this.CreateConstValueAttribute(0.03f, Stats.AbilityRecoveryMultiplier));
        result.BaseAttributeValues.Add(this.CreateConstValueAttribute(1, Stats.PetDurationIncrease));
        result.BaseAttributeValues.Add(this.CreateConstValueAttribute(10, Stats.MinimumPhysBaseDmg));
        result.BaseAttributeValues.Add(this.CreateConstValueAttribute(15, Stats.MaximumPhysBaseDmg));
        result.BaseAttributeValues.Add(this.CreateConstValueAttribute(70, Stats.AttackRatePvm));
        result.BaseAttributeValues.Add(this.CreateConstValueAttribute(95, Stats.DefenseRatePvm));
        result.BaseAttributeValues.Add(this.CreateConstValueAttribute(MovementSpeedConstants.RunningGearMovementSpeed, Stats.MovementSpeed, AggregateType.Maximum));
        result.BaseAttributeValues.Add(this.CreateConstValueAttribute(MovementSpeedConstants.RunningGearMovementSpeed, Stats.MovementSpeedUnderwater, AggregateType.Maximum));

        result.BaseAttributeValues.Add(this.CreateConstValueAttribute(180, Stats.RavenMinimumDamage));
        result.BaseAttributeValues.Add(this.CreateConstValueAttribute(200, Stats.RavenMaximumDamage));
        result.BaseAttributeValues.Add(this.CreateConstValueAttribute(20, Stats.RavenAttackSpeed));
        result.BaseAttributeValues.Add(this.CreateConstValueAttribute(1000, Stats.RavenAttackRate));
        result.BaseAttributeValues.Add(this.CreateConstValueAttribute(0.3f, Stats.RavenCriticalDamageChance));

        this.AddCommonBaseAttributeValues(result.BaseAttributeValues, isMaster);

        return result;
    }

    private CharacterClass CreateLordEmperor()
    {
        var result = this.CreateDarkLord(CharacterClassNumber.LordEmperor, "Lord Emperor", true, null, false);
        result.StatAttributes.Add(this.CreateStatAttributeDefinition(Stats.MasterLevel, 0, false));
        return result;
    }
}