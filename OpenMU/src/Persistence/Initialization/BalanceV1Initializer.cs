// <copyright file="BalanceV1Initializer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.ItemCrafting;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Installs the isolated balance-v1 runtime core into a newly initialized Season 6 configuration.
/// </summary>
public sealed class BalanceV1Initializer : InitializerBase
{
    private static readonly (byte CraftingNumber, byte TargetLevel)[] LevelUpgradeCraftings =
    [
        (3, 10), (4, 11), (22, 12), (23, 13), (49, 14), (50, 15),
    ];

    private static readonly (byte Group, short Number)[] PotionIdentifiers =
    [
        (14, 1), (14, 2), (14, 3), (14, 4), (14, 5), (14, 6), (14, 35), (14, 36), (14, 37),
    ];

    private readonly BalanceV1.ExperienceProfile _profile;

    /// <summary>Initializes a new instance of the <see cref="BalanceV1Initializer"/> class.</summary>
    /// <param name="context">The writable configuration context.</param>
    /// <param name="gameConfiguration">The complete Season 6 configuration.</param>
    /// <param name="profile">The explicitly selected experience-rate profile.</param>
    public BalanceV1Initializer(
        IContext context,
        GameConfiguration gameConfiguration,
        BalanceV1.ExperienceProfile profile)
        : base(context, gameConfiguration)
    {
        this._profile = profile;
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        BalanceV1.ValidateProfileMarkers(this.GameConfiguration);
        if (SoloBalance.IsEnabled(this.GameConfiguration))
        {
            throw new InvalidOperationException(
                "Cannot install balance-v1 because this configuration already uses the mutually exclusive Solo profile.");
        }

        if (BalanceV1.IsEnabled(this.GameConfiguration))
        {
            _ = ValidateCoreConfiguration(this.GameConfiguration, this._profile);
            this.ConfigureEarlyMerchantStock();
            return;
        }

        BalanceV1ContentRank.ValidateConfiguration(this.GameConfiguration);
        this.ConfigureProgression();
        this.ConfigureLevelEnhancement();
        this.ConfigureEarlyMerchantStock();

        // The marker is deliberately added last and is saved in the same configuration transaction.
        var marker = this.Context.CreateNew<AttributeDefinition>(
            BalanceV1.ProfileAttributeId,
            "balance-v1 profile version",
            $"OpenMU candidate balance-v1 ({BalanceV1.GetProfileId(this._profile)} experience profile).");
        this.GameConfiguration.Attributes.Add(marker);
        this.GameConfiguration.GlobalBaseAttributeValues.Add(
            this.Context.CreateNew<ConstValueAttribute>(1f, marker, AggregateType.AddRaw));

        _ = ValidateCoreConfiguration(this.GameConfiguration, this._profile);
    }

    /// <summary>Validates every configuration value required by the installed runtime core.</summary>
    /// <param name="configuration">The installed configuration.</param>
    /// <param name="expectedProfile">An optional profile requested for this launch.</param>
    /// <returns>The installed experience profile.</returns>
    public static BalanceV1.ExperienceProfile ValidateCoreConfiguration(
        GameConfiguration configuration,
        BalanceV1.ExperienceProfile? expectedProfile = null)
    {
        BalanceV1.ValidateProfileMarkers(configuration);
        if (!BalanceV1.IsEnabled(configuration))
        {
            throw new InvalidOperationException("The configuration has no completed balance-v1 profile marker.");
        }

        if (configuration.MaximumLevel != BalanceV1.NormalLevelCap
            || configuration.MaximumMasterLevel != BalanceV1.MasterLevelCap)
        {
            throw new InvalidOperationException(
                $"balance-v1 requires level caps {BalanceV1.NormalLevelCap}/{BalanceV1.MasterLevelCap}.");
        }

        var installedProfile = GetInstalledProfile(configuration);
        if (expectedProfile is { } requested && installedProfile != requested)
        {
            throw new InvalidOperationException(
                $"balance-v1 is already installed with profile '{BalanceV1.GetProfileId(installedProfile)}', "
                + $"but this launch requested '{BalanceV1.GetProfileId(requested)}'. Profile conversion is not automatic.");
        }

        if (configuration.CharacterClasses.Count != 18)
        {
            throw new InvalidOperationException(
                $"balance-v1 requires all 18 Season 6 character stages, but found {configuration.CharacterClasses.Count}.");
        }

        foreach (var characterClass in configuration.CharacterClasses)
        {
            var points = characterClass.StatAttributes.SingleOrDefault(
                attribute => attribute.Attribute?.Id == Stats.PointsPerLevelUp.Id);
            if (points?.BaseValue != BalanceV1.PointsPerLevel)
            {
                throw new InvalidOperationException(
                    $"Character class {characterClass.Number} does not grant exactly {BalanceV1.PointsPerLevel} points per level.");
            }
        }

        if (configuration.GlobalBaseAttributeValues.Any(
                value => value.Definition?.Id == Stats.PointsPerLevelUp.Id && value.Value != 0))
        {
            throw new InvalidOperationException(
                "balance-v1 cannot activate while a global PointsPerLevelUp modifier is installed.");
        }

        foreach (var (group, number) in PotionIdentifiers)
        {
            if (!configuration.Items.Any(item => item.Group == group && item.Number == number)
                || !BalanceV1.TryGetPotionRule(group, number, out _))
            {
                throw new InvalidOperationException($"balance-v1 potion mapping {group}:{number} is incomplete.");
            }
        }

        BalanceV1ContentRank.ValidateConfiguration(configuration);
        ValidateLevelEnhancement(configuration);
        return installedProfile;
    }

    private static BalanceV1.ExperienceProfile GetInstalledProfile(GameConfiguration configuration)
    {
        foreach (var profile in Enum.GetValues<BalanceV1.ExperienceProfile>())
        {
            var multiplier = BalanceV1.GetExperienceMultiplier(profile);
            if (Math.Abs(configuration.ExperienceRate - multiplier) < 0.0001f
                && Math.Abs(configuration.MasterExperienceRate - multiplier) < 0.0001f)
            {
                return profile;
            }
        }

        throw new InvalidOperationException(
            $"balance-v1 normal/master experience rates must match a supported profile; found "
            + $"{configuration.ExperienceRate}/{configuration.MasterExperienceRate}.");
    }

    private static void ValidateLevelEnhancement(GameConfiguration configuration)
    {
        var chaosMachine = configuration.Monsters.SingleOrDefault(monster => monster.NpcWindow == NpcWindow.ChaosMachine)
            ?? throw new InvalidOperationException("balance-v1 requires exactly one Chaos Machine definition.");

        foreach (var (craftingNumber, targetLevel) in LevelUpgradeCraftings)
        {
            var crafting = chaosMachine.ItemCraftings.SingleOrDefault(candidate => candidate.Number == craftingNumber);
            var settings = crafting?.SimpleCraftingSettings
                ?? throw new InvalidOperationException($"balance-v1 enhancement +{targetLevel} is missing.");
            var expectedPercent = checked((byte)Math.Round(BalanceV1.GetUpgradeStep(targetLevel).Chance * 100));
            if (settings.SuccessPercent != expectedPercent
                || settings.MaximumSuccessPercent != expectedPercent
                || settings.SuccessPercentageAdditionForLuck != 0
                || settings.SuccessPercentageAdditionForExcellentItem != 0
                || settings.SuccessPercentageAdditionForAncientItem != 0
                || settings.SuccessPercentageAdditionForGuardianItem != 0
                || settings.SuccessPercentageAdditionForSocketItem != 0)
            {
                throw new InvalidOperationException($"balance-v1 enhancement +{targetLevel} has an incomplete success-rate override.");
            }

            var baseItem = settings.RequiredItems.SingleOrDefault(
                requirement => requirement.Reference > 0 && requirement.MinimumItemLevel == targetLevel - 1);
            if (baseItem?.FailResult != MixResult.StaysAsIs)
            {
                throw new InvalidOperationException(
                    $"balance-v1 enhancement +{targetLevel} must preserve the base item on failure.");
            }
        }
    }

    private void ConfigureProgression()
    {
        var multiplier = BalanceV1.GetExperienceMultiplier(this._profile);
        this.GameConfiguration.MaximumLevel = BalanceV1.NormalLevelCap;
        this.GameConfiguration.MaximumMasterLevel = BalanceV1.MasterLevelCap;
        this.GameConfiguration.ExperienceRate = multiplier;
        this.GameConfiguration.MasterExperienceRate = multiplier;
        this.GameConfiguration.PreventExperienceOverflow = false;
        this.GameConfiguration.ShouldDropMoney = true;
        this.GameConfiguration.ClampMoneyOnPickup = true;

        if (this.GameConfiguration.GlobalBaseAttributeValues.Any(
                value => value.Definition?.Id == Stats.PointsPerLevelUp.Id && value.Value != 0))
        {
            throw new InvalidOperationException(
                "Cannot install balance-v1 over a configuration with a global PointsPerLevelUp modifier.");
        }

        foreach (var characterClass in this.GameConfiguration.CharacterClasses)
        {
            var points = characterClass.StatAttributes.SingleOrDefault(
                attribute => attribute.Attribute?.Id == Stats.PointsPerLevelUp.Id)
                ?? throw new InvalidOperationException(
                    $"Character class {characterClass.Number} has no PointsPerLevelUp stat definition.");
            points.BaseValue = BalanceV1.PointsPerLevel;
        }
    }

    private void ConfigureLevelEnhancement()
    {
        var chaosMachine = this.GameConfiguration.Monsters.Single(
            monster => monster.NpcWindow == NpcWindow.ChaosMachine);
        foreach (var (craftingNumber, targetLevel) in LevelUpgradeCraftings)
        {
            var settings = chaosMachine.ItemCraftings.Single(crafting => crafting.Number == craftingNumber)
                .SimpleCraftingSettings
                ?? throw new InvalidOperationException($"Enhancement +{targetLevel} has no simple crafting settings.");
            var successPercent = checked((byte)Math.Round(BalanceV1.GetUpgradeStep(targetLevel).Chance * 100));
            settings.SuccessPercent = successPercent;
            settings.MaximumSuccessPercent = successPercent;
            settings.SuccessPercentageAdditionForLuck = 0;
            settings.SuccessPercentageAdditionForExcellentItem = 0;
            settings.SuccessPercentageAdditionForAncientItem = 0;
            settings.SuccessPercentageAdditionForGuardianItem = 0;
            settings.SuccessPercentageAdditionForSocketItem = 0;

            var baseItem = settings.RequiredItems.Single(
                requirement => requirement.Reference > 0 && requirement.MinimumItemLevel == targetLevel - 1);
            baseItem.FailResult = MixResult.StaysAsIs;
        }
    }

    private void ConfigureEarlyMerchantStock()
    {
        this.AddEarlyWeapon(251, 0, 32, true);
        this.AddEarlyWeapon(254, 5, 0, false);
    }

    private void AddEarlyWeapon(short merchantNumber, byte group, short number, bool hasSkill)
    {
        var store = this.GameConfiguration.Monsters.Single(monster => monster.Number == merchantNumber).MerchantStore
            ?? throw new InvalidOperationException($"balance-v1 starter merchant {merchantNumber} has no store.");
        var definition = this.GameConfiguration.Items.Single(item => item.Group == group && item.Number == number);
        if (store.Items.Any(item => item.Definition == definition && item.Level == 0 && item.HasSkill == hasSkill && item.ItemOptions.Count == 0))
        {
            return;
        }

        // Stores are the same 8x15 grid used by the original and Solo initializers.
        for (var slot = 0; slot < 120; slot++)
        {
            var x = slot % 8;
            var y = slot / 8;
            if (x + definition.Width > 8 || y + definition.Height > 15
                || store.Items.Any(item => item.ItemSlot % 8 < x + definition.Width
                    && (item.ItemSlot % 8) + item.Definition!.Width > x
                    && item.ItemSlot / 8 < y + definition.Height
                    && (item.ItemSlot / 8) + item.Definition.Height > y))
            {
                continue;
            }

            var item = this.Context.CreateNew<Item>();
            item.Definition = definition;
            item.HasSkill = hasSkill;
            item.Durability = definition.Durability;
            item.ItemSlot = (byte)slot;
            store.Items.Add(item);
            return;
        }

        throw new InvalidOperationException($"balance-v1 starter merchant {merchantNumber} has no room for {group}:{number}.");
    }
}
