// <copyright file="SoloBalanceInitializer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization;

using System.Text.Json.Nodes;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;

/// <summary>
/// One-time, opt-in conversion of a Season 6 configuration to a solo progression profile.
/// Character inventories, currency, experience and allocated stats are not rewritten.
/// </summary>
public sealed class SoloBalanceInitializer : InitializerBase
{
    private const float ExperienceMultiplier = 8;
    private const float MasterExperienceMultiplier = 5;
    private const int StoreWidth = 8;
    private const int StoreHeight = 15;

    /// <summary>Initializes a new instance of the <see cref="SoloBalanceInitializer"/> class.</summary>
    /// <param name="context">The writable configuration context.</param>
    /// <param name="gameConfiguration">The complete Season 6 configuration.</param>
    public SoloBalanceInitializer(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        if (SoloBalance.IsEnabled(this.GameConfiguration))
        {
            return;
        }

        this.ConfigureProgression();
        this.ConfigureMonsters();
        this.ConfigureDrops();
        this.ConfigureCraftingAndQuests();
        this.ConfigureSupplies();
        this.ConfigureJewelUpgrades();

        // Written last and saved with the same configuration transaction.
        var marker = this.Context.CreateNew<AttributeDefinition>(
            SoloBalance.ProfileAttributeId, "Solo profile version", "Local solo balance, version 1.");
        this.GameConfiguration.Attributes.Add(marker);
        this.GameConfiguration.GlobalBaseAttributeValues.Add(
            this.Context.CreateNew<ConstValueAttribute>(1f, marker, AggregateType.AddRaw));
    }

    private void ConfigureProgression()
    {
        var config = this.GameConfiguration;
        config.ExperienceRate = ExperienceMultiplier;
        config.MasterExperienceRate = MasterExperienceMultiplier;
        config.PreventExperienceOverflow = false;
        config.ClampMoneyOnPickup = true;
        config.ShouldDropMoney = false;
        config.RecoveryInterval = 2000;
        config.ItemDropDuration = TimeSpan.FromMinutes(3);
        config.ExcellentItemDropLevelDelta = 10;
        config.LetterSendPrice = (int)SoloBalance.ScalePrice(config.LetterSendPrice);
        config.DamagePerOneItemDurability *= 5;
        config.DamagePerOnePetDurability *= 10;
        config.HitsPerOneItemDurability *= 5;
        var levelPointBonus = this.Context.CreateNew<AttributeDefinition>(
            SoloBalance.LevelUpPointBonusAttributeId, "Solo level point bonus", "Additional points per normal level in the solo profile.");
        config.Attributes.Add(levelPointBonus);
        this.AddBaseAttribute(levelPointBonus, 3);
        this.AddBaseAttribute(Stats.MaximumHealth, 100);
        this.AddBaseAttribute(Stats.HealthAfterMonsterKillMultiplier, 0.03f);
        this.AddBaseAttribute(Stats.ManaAfterMonsterKillMultiplier, 0.03f);
        var moneyRate = config.GlobalBaseAttributeValues.Single(a => a.Definition == Stats.MoneyAmountRate);
        config.GlobalBaseAttributeValues.Remove(moneyRate);
        this.AddBaseAttribute(Stats.MoneyAmountRate, 0.25f);

        foreach (var characterClass in config.CharacterClasses.Where(c => c.CanGetCreated))
        {
            characterClass.LevelRequirementByCreation = 0;
        }

        foreach (var npc in config.Monsters)
        {
            foreach (var buff in npc.Buffs)
            {
                buff.MaximumLevel = config.MaximumLevel;
            }
        }
    }

    private void AddBaseAttribute(AttributeDefinition attribute, float value) =>
        this.GameConfiguration.GlobalBaseAttributeValues.Add(
            this.Context.CreateNew<ConstValueAttribute>(value, attribute.GetPersistent(this.GameConfiguration), AggregateType.AddRaw));

    private void ConfigureMonsters()
    {
        foreach (var monster in this.GameConfiguration.Monsters.Where(m =>
                     m.ObjectKind is NpcObjectKind.Monster or NpcObjectKind.Trap or NpcObjectKind.Destructible))
        {
            foreach (var attribute in monster.Attributes)
            {
                var definition = attribute.AttributeDefinition;
                if (definition == Stats.MaximumHealth)
                {
                    attribute.Value = Compress(attribute.Value, 0.45f, 20000, 300000);
                }
                else if (definition == Stats.MinimumPhysBaseDmg || definition == Stats.MaximumPhysBaseDmg)
                {
                    attribute.Value = Compress(attribute.Value, 0.40f, 400, 1500);
                }
                else if (definition == Stats.DefenseBase || definition == Stats.DefenseRatePvm)
                {
                    attribute.Value = Compress(attribute.Value, 0.45f, 300, 1000);
                }
                else if (definition == Stats.AttackRatePvm)
                {
                    attribute.Value = Compress(attribute.Value, 0.7f, 1500, 4000);
                }
            }
        }
    }

    private static float Compress(float value, float multiplier, float knee, float maximum)
    {
        if (value <= 0)
        {
            return value;
        }

        var scaled = value * multiplier;
        return Math.Clamp(scaled <= knee ? scaled : knee + MathF.Sqrt((scaled - knee) * knee), 1, maximum);
    }

    private void ConfigureDrops()
    {
        // Groups are shared by maps, monsters and quests: visit each definition only once.
        foreach (var group in this.GameConfiguration.DropItemGroups)
        {
            group.Chance = group.ItemType switch
            {
                SpecialItemType.Excellent => Math.Max(group.Chance, 0.02),
                SpecialItemType.Jewel => Math.Max(group.Chance, 0.03),
                SpecialItemType.Ancient => Math.Max(group.Chance, 0.10),
                SpecialItemType.SocketItem => Math.Max(group.Chance, 0.02),
                _ when group.Chance > 0 && group.Chance < 0.01 => Math.Min(0.10, group.Chance * 10),
                _ => group.Chance,
            };
        }
    }

    private void ConfigureCraftingAndQuests()
    {
        var config = this.GameConfiguration;
        foreach (var settings in config.Monsters.SelectMany(m => m.ItemCraftings)
                     .Select(c => c.SimpleCraftingSettings).OfType<DataModel.Configuration.ItemCrafting.SimpleCraftingSettings>().Distinct())
        {
            settings.SuccessPercent = 100;
            settings.MaximumSuccessPercent = 100;
            settings.SuccessPercentageAdditionForAncientItem = 0;
            settings.SuccessPercentageAdditionForGuardianItem = 0;
            settings.SuccessPercentageAdditionForSocketItem = 0;
        }

        foreach (var quest in config.Monsters.SelectMany(m => m.Quests).Distinct())
        {
            quest.RequiredStartMoney = (int)SoloBalance.ScalePrice(quest.RequiredStartMoney);
        }

        foreach (var warp in config.WarpList)
        {
            warp.Costs = (int)SoloBalance.ScalePrice(warp.Costs);
        }

        foreach (var miniGame in config.MiniGameDefinitions)
        {
            miniGame.EntranceFee = (int)SoloBalance.ScalePrice(miniGame.EntranceFee);
            miniGame.EnterDuration = TimeSpan.FromSeconds(30);
        }
    }

    private void ConfigureSupplies()
    {
        var config = this.GameConfiguration;
        var supplies = config.Monsters.Single(m => m.Number == 253).MerchantStore!;
        (byte Group, short Number)[] materials =
        [
            (14, 13), (14, 14), (12, 15), (14, 16), (14, 22), (14, 31),
            (14, 41), (14, 42), (14, 43), (14, 44), (13, 14), (13, 31),
            (13, 32), (13, 33), (13, 34), (13, 35), (13, 36), (13, 52),
            (13, 53), (13, 2), (13, 3), (13, 37),
        ];
        foreach (var id in materials)
        {
            this.AddStoreItem(supplies, config.Items.Single(i => i.Group == id.Group && i.Number == id.Number), 0);
        }

        this.AddStoreItem(supplies, config.Items.Single(i => i.Group == 13 && i.Number == 31), 1);

        var bar = config.Monsters.Single(m => m.Number == 255).MerchantStore!;
        foreach (var game in config.MiniGameDefinitions.Where(g => g.TicketItem is not null))
        {
            this.AddStoreItem(bar, game.TicketItem!, (byte)game.TicketItemLevel);
        }

        var skillStores = new short[] { 254, 255, 253, 230 }
            .Select(number => config.Monsters.Single(m => m.Number == number).MerchantStore!).ToArray();
        foreach (var skillItem in config.Items.Where(i => i.Skill is not null && i.Group is 12 or 15 && i.ItemSlot is null))
        {
            if (!skillStores.Any(store => store.Items.Any(i => i.Definition == skillItem))
                && !skillStores.Any(store => this.TryAddStoreItem(store, skillItem, 0)))
            {
                throw new InvalidOperationException($"Solo skill stores are full; cannot add {skillItem}.");
            }
        }
    }

    private void AddStoreItem(ItemStorage store, ItemDefinition definition, byte level)
    {
        if (!this.TryAddStoreItem(store, definition, level))
        {
            throw new InvalidOperationException($"Solo supply store is full; cannot add {definition}.");
        }
    }

    private bool TryAddStoreItem(ItemStorage store, ItemDefinition definition, byte level)
    {
        if (store.Items.Any(i => i.Definition == definition && i.Level == level))
        {
            return true;
        }

        for (var slot = 0; slot < StoreWidth * StoreHeight; slot++)
        {
            var x = slot % StoreWidth;
            var y = slot / StoreWidth;
            if (x + definition.Width > StoreWidth || y + definition.Height > StoreHeight
                || store.Items.Any(i => i.ItemSlot % StoreWidth < x + definition.Width
                    && i.ItemSlot % StoreWidth + i.Definition!.Width > x
                    && i.ItemSlot / StoreWidth < y + definition.Height
                    && i.ItemSlot / StoreWidth + i.Definition.Height > y))
            {
                continue;
            }

            var item = this.Context.CreateNew<Item>();
            item.Definition = definition;
            item.Level = level;
            item.ItemSlot = (byte)slot;
            item.Durability = Math.Max((byte)1, definition.Durability);
            store.Items.Add(item);
            return true;
        }

        return false;
    }

    private void ConfigureJewelUpgrades()
    {
        foreach (var plugin in this.GameConfiguration.PlugInConfigurations.Where(p =>
                     p.TypeId == typeof(SoulJewelConsumeHandlerPlugIn).GUID))
        {
            var json = JsonNode.Parse(plugin.CustomConfiguration ?? "{}")!.AsObject();
            json["SuccessRatePercentage"] = 100;
            plugin.CustomConfiguration = json.ToJsonString();
        }
    }
}
