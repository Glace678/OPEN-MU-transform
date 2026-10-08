// <copyright file="DefaultDropGenerator.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// The default drop generator.
/// </summary>
public class DefaultDropGenerator : IDropGenerator
{
    /// <summary>
    /// The amount of money which is dropped at least, and added to the gained experience.
    /// </summary>
    private const int BaseMoneyDrop = 7;
    private const int DropLevelMaxGap = 12;
    private const int SkillDropChancePercent = 50;

    private const byte DefaultMaxItemOptionLevelDrop = 3;
    private const byte MinItemOptionLevelDrop = 1;
    private const byte MaxItemOptionLevelDrop = 4;

    private readonly IRandomizer _randomizer;
    private readonly IList<ItemDefinition> _ancientItems;
    private readonly IList<ItemDefinition> _droppableItems;
    private readonly IList<ItemDefinition>?[] _droppableItemsPerMonsterLevel = new IList<ItemDefinition>?[byte.MaxValue + 1];
    private readonly IList<ItemDefinition>?[] _droppableSocketItemsPerMonsterLevel = new IList<ItemDefinition>?[byte.MaxValue + 1];

    private readonly byte _maxItemOptionLevelDrop;
    private readonly byte _excellentItemDropLevelDelta;
    private readonly bool _balanceV1Enabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDropGenerator" /> class.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="randomizer">The randomizer.</param>
    public DefaultDropGenerator(GameConfiguration config, IRandomizer randomizer)
    {
        BalanceV1.ValidateProfileMarkers(config);
        this._balanceV1Enabled = BalanceV1.IsEnabled(config);
        this._excellentItemDropLevelDelta = config.ExcellentItemDropLevelDelta;
        this._randomizer = randomizer;
        this._maxItemOptionLevelDrop = IsValidOptionLevelDrop(config.MaximumItemOptionLevelDrop)
            ? config.MaximumItemOptionLevelDrop
            : DefaultMaxItemOptionLevelDrop;
        this._droppableItems = config.Items.Where(i => i.DropsFromMonsters).ToList();
        this._ancientItems = this._droppableItems.Where(
            i => i.PossibleItemSetGroups.Any(
                g => g.Options?.PossibleOptions.Any(
                    o => object.Equals(o.OptionType, ItemOptionTypes.AncientOption)) ?? false))
            .ToList();
    }

    /// <inheritdoc/>
    public async ValueTask<(IEnumerable<Item> Items, uint? Money)> GenerateItemDropsAsync(MonsterDefinition monster, int gainedExperience, Player player)
    {
        var character = player.SelectedCharacter;
        var map = player.CurrentMap?.Definition;
        if (map is null || character is null)
        {
            return ([], null);
        }

        // The drop groups are local to this call: previously they were shared instance fields
        // guarded by a global lock held across awaits (incl. the party quest-group fetch), which
        // serialized all drops on the whole server. Local lists need no lock and can't be
        // corrupted by concurrent kills.
        var guaranteedDropGroups = new List<DropItemGroup>(16);
        var chanceDropGroups = new List<DropItemGroup>(64);

        if (this._balanceV1Enabled && monster.ObjectKind == NpcObjectKind.Monster)
        {
            return await this.GenerateBalanceV1DropsAsync(monster, player, map).ConfigureAwait(false);
        }

        if (monster.ObjectKind == NpcObjectKind.Destructible)
        {
            this.PartitionDropGroups(guaranteedDropGroups, chanceDropGroups, monster.DropItemGroups ?? []);
        }
        else
        {
            this.PartitionDropGroups(guaranteedDropGroups, chanceDropGroups, monster.DropItemGroups ?? []);
            this.PartitionDropGroups(guaranteedDropGroups, chanceDropGroups, character.DropItemGroups ?? [], monster);
            this.PartitionDropGroups(guaranteedDropGroups, chanceDropGroups, map.DropItemGroups ?? [], monster);
            this.PartitionDropGroups(guaranteedDropGroups, chanceDropGroups, await GetQuestItemGroupsAsync(player).ConfigureAwait(false) ?? [], monster);
        }

        var (droppedItems, money) = this.GenerateDrops(monster, gainedExperience, guaranteedDropGroups, chanceDropGroups);
        return (droppedItems ?? Enumerable.Empty<Item>(), money > 0 ? money : null);
    }

    /// <inheritdoc/>
    public Item? GenerateItemDrop(DropItemGroup selectedGroup)
    {
        return this.GenerateItemDrop(selectedGroup, selectedGroup.PossibleItems);
    }

    /// <inheritdoc/>
    public (Item? Item, uint? Money, ItemDropEffect DropEffect) GenerateItemDrop(IEnumerable<DropItemGroup> groups)
    {
        var group = this.SelectRandomGroup(groups.OrderBy(group => group.Chance), 1.0);
        if (group is null)
        {
            return (null, null, ItemDropEffect.Undefined);
        }

        var dropEffect = ItemDropEffect.Undefined;
        if (group is ItemDropItemGroup itemDropItemGroup)
        {
            dropEffect = itemDropItemGroup.DropEffect;

            if (group.ItemType == SpecialItemType.Money)
            {
                return (null, (uint)itemDropItemGroup.MoneyAmount, dropEffect);
            }
        }

        return (this.GenerateItemDrop(group), null, dropEffect);
    }

    /// <summary>
    /// Gets a random item.
    /// </summary>
    /// <param name="monsterLevel">The monster level.</param>
    /// <param name="isSocketItem">If set to <c>true</c>, it selects only socket items.</param>
    /// <returns>A random item.</returns>
    protected Item? GenerateRandomItem(int monsterLevel, bool isSocketItem)
    {
        var possible = this.GetPossibleList(monsterLevel, isSocketItem);
        var item = this.GenerateRandomItem(possible);
        if (item is null)
        {
            return null;
        }

        item.Level = GetItemLevelByMonsterLevel(item.Definition!, monsterLevel);
        item.Durability = item.GetMaximumDurabilityOfOnePiece();
        return item;
    }

    /// <summary>
    /// Applies random options to the item.
    /// </summary>
    /// <param name="item">The item.</param>
    protected void ApplyRandomOptions(Item item)
    {
        foreach (var option in item.Definition!.PossibleItemOptions.Where(o =>
            o.AddsRandomly &&
            !o.PossibleOptions.Any(po => object.Equals(po.OptionType, ItemOptionTypes.Excellent))))
        {
            this.ApplyOption(item, option);
        }

        if (item.Definition.MaximumSockets > 0)
        {
            item.SocketCount = this._randomizer.NextInt(1, item.Definition.MaximumSockets + 1);
        }

        if (item.CanHaveSkill())
        {
            item.HasSkill = this._randomizer.NextRandomBool(SkillDropChancePercent);
        }
    }

    /// <summary>
    /// Gets a random excellent item.
    /// </summary>
    /// <param name="monsterLevel">The monster level, if it's a monster drop.</param>
    /// <param name="possibleItems">The possible items, if the drop is from an item box (e.g. box of kundun).</param>
    /// <returns>A random excellent item.</returns>
    protected Item? GenerateRandomExcellentItem(int monsterLevel = 0, ICollection<ItemDefinition>? possibleItems = null)
    {
        if (monsterLevel < this._excellentItemDropLevelDelta && possibleItems is null)
        {
            return null;
        }

        var possible = possibleItems ?? this.GetPossibleList(monsterLevel - this._excellentItemDropLevelDelta);
        var item = this.GenerateRandomItem(possible);
        if (item is null)
        {
            return null;
        }

        item.HasSkill = item.CanHaveSkill(); // every excellent item got skill

        this.AddRandomExcOptions(item);
        item.Durability = item.GetMaximumDurabilityOfOnePiece();
        return item;
    }

    /// <summary>
    /// Gets a random ancient item.
    /// </summary>
    /// <returns>A random ancient item.</returns>
    protected Item? GenerateRandomAncient()
    {
        var item = this.GenerateRandomItem(this._ancientItems);
        if (item is null)
        {
            return null;
        }

        item.HasSkill = item.CanHaveSkill(); // every ancient item got skill

        this.ApplyRandomAncientOption(item);
        item.Durability = item.GetMaximumDurabilityOfOnePiece();
        return item;
    }

    private static byte GetItemLevelByMonsterLevel(ItemDefinition itemDefinition, int monsterLevel)
    {
        return Math.Min((byte)((monsterLevel - itemDefinition.DropLevel) / 3), itemDefinition.MaximumItemLevel);
    }

    private static async ValueTask<IEnumerable<DropItemGroup>> GetQuestItemGroupsAsync(Player player)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return [];
        }

        if (player.Party is { } party)
        {
            return await party.GetQuestDropItemGroupsAsync(player).ConfigureAwait(false);
        }

        return character.GetQuestDropItemGroups();
    }

    private static bool IsGroupRelevant(MonsterDefinition monsterDefinition, DropItemGroup group)
    {
        if (group.MinimumMonsterLevel.HasValue && monsterDefinition[Stats.Level] < group.MinimumMonsterLevel)
        {
            return false;
        }

        if (group.MaximumMonsterLevel.HasValue && monsterDefinition[Stats.Level] > group.MaximumMonsterLevel)
        {
            return false;
        }

        if (group.Monster is { } monster && !monster.Equals(monsterDefinition))
        {
            return false;
        }

        return true;
    }

    private static bool IsValidOptionLevelDrop(byte value)
        => value is >= MinItemOptionLevelDrop and <= MaxItemOptionLevelDrop;

    private static bool CanDropAtMonsterLevel(ItemDefinition itemDefinition, int monsterLevel)
    {
        if (itemDefinition.DropLevel > monsterLevel)
        {
            return false;
        }

        return itemDefinition.MaximumDropLevel is not { } maxDropLevel || monsterLevel <= maxDropLevel;
    }

    private async ValueTask<(IEnumerable<Item> Items, uint? Money)> GenerateBalanceV1DropsAsync(
        MonsterDefinition monster,
        Player player,
        GameMapDefinition map)
    {
        var contentRank = Math.Max(1, BalanceV1ContentRank.ResolveRank(monster, map));

        // Content ranks reach 400; original item-tier caches are indexed by the original monster level.
        var originalDropLevel = (int)monster[Stats.Level];

        // The designed "excellent item guaranteed after 250 eligible kills (rank >= 80)" pity is a
        // separate kill counter; jewel upgrade pity is honoured via Item.JewelUpgradeFailures in
        // UpgradeItemLevelJewelConsumeHandlerPlugIn.
        var roll = BalanceV1.RollLoot(
            contentRank,
            this._randomizer.NextDouble(),
            this._randomizer.NextDouble(),
            this._randomizer.NextDouble());
        var items = new List<Item>(3);

        var equipment = roll.Equipment switch
        {
            BalanceV1.EquipmentDrop.Common => this.GenerateRandomItem(originalDropLevel, false),
            BalanceV1.EquipmentDrop.Excellent => this.GenerateBalanceV1ExcellentItem(originalDropLevel),
            BalanceV1.EquipmentDrop.Ancient => this.GenerateRandomAncient(),
            BalanceV1.EquipmentDrop.Socket => this.GenerateRandomItem(originalDropLevel, true),
            _ => null,
        };
        if (equipment is not null)
        {
            items.Add(equipment);
        }

        if (roll.Jewel
            && this.SelectBalanceV1JewelGroup(monster, player) is { } jewelGroup
            && this.GenerateItemFromGroup(monster, jewelGroup) is { } jewel)
        {
            items.Add(jewel);
        }

        foreach (var questGroup in (await GetQuestItemGroupsAsync(player).ConfigureAwait(false))
                     .Where(group => IsGroupRelevant(monster, group)))
        {
            if (questGroup.Chance >= 1 || this._randomizer.NextDouble() < questGroup.Chance)
            {
                var questItem = this.GenerateItemFromGroup(monster, questGroup);
                if (questItem is not null)
                {
                    items.Add(questItem);
                }
            }
        }

        var money = roll.Money
            ? BalanceV1.CalculateZen(contentRank, monster.Number)
            : default(uint?);
        return (items, money);
    }

    private Item? GenerateBalanceV1ExcellentItem(int originalDropLevel)
    {
        var itemTier = originalDropLevel - this._excellentItemDropLevelDelta;
        var possible = this.GetPossibleList(itemTier)?.Where(definition => definition.PossibleItemOptions.Any(
            option => option.PossibleOptions.Any(value => object.Equals(value.OptionType, ItemOptionTypes.Excellent)))).ToList();
        return possible is { Count: > 0 }
            ? this.GenerateRandomExcellentItem(originalDropLevel, possible)
            : null;
    }

    private DropItemGroup? SelectBalanceV1JewelGroup(MonsterDefinition monster, Player player)
    {
        var groups = (monster.DropItemGroups ?? [])
            .Concat(player.SelectedCharacter?.DropItemGroups ?? [])
            .Concat(player.CurrentMap?.Definition.DropItemGroups ?? [])
            .Where(group => group.ItemType == SpecialItemType.Jewel
                            && group.PossibleItems?.Count > 0
                            && IsGroupRelevant(monster, group))
            .Distinct()
            .ToList();
        if (groups.Count == 0)
        {
            return null;
        }

        var totalWeight = groups.Sum(group => Math.Max(0, group.Chance));
        if (totalWeight <= 0)
        {
            return groups[this._randomizer.NextInt(0, groups.Count)];
        }

        var threshold = this._randomizer.NextDouble() * totalWeight;
        foreach (var group in groups)
        {
            threshold -= Math.Max(0, group.Chance);
            if (threshold <= 0)
            {
                return group;
            }
        }

        return groups[^1];
    }

    private (IList<Item>? Items, uint Money) GenerateDrops(
        MonsterDefinition monster,
        int gainedExperience,
        IList<DropItemGroup> guaranteedDropGroups,
        IList<DropItemGroup> chanceDropGroups)
    {
        uint money = 0;
        List<Item>? droppedItems = null;
        var remainingDrops = monster.NumberOfMaximumItemDrops;

        // Guaranteed groups.
        foreach (var group in guaranteedDropGroups)
        {
            if (remainingDrops <= 0)
            {
                break;
            }

            var item = this.GenerateItemDropOrMoney(monster, group, gainedExperience, out var droppedMoney);
            if (item is not null)
            {
                droppedItems ??= new List<Item>(monster.NumberOfMaximumItemDrops);
                droppedItems.Add(item);
            }

            if (droppedMoney is not null)
            {
                money += droppedMoney.Value;
            }

            remainingDrops--;
        }

        // Chance based groups.
        if (remainingDrops > 0 && chanceDropGroups.Count > 0)
        {
            double totalChance = 0;
            foreach (var group in chanceDropGroups)
            {
                totalChance += group.Chance;
            }

            for (int i = 0; i < remainingDrops; i++)
            {
                var group = this.SelectRandomGroup(chanceDropGroups, totalChance);
                if (group is null)
                {
                    continue;
                }

                var item = this.GenerateItemDropOrMoney(monster, group, gainedExperience, out var droppedMoney);
                if (item is not null)
                {
                    droppedItems ??= new List<Item>(monster.NumberOfMaximumItemDrops);
                    droppedItems.Add(item);
                }

                if (droppedMoney is not null)
                {
                    money += droppedMoney.Value;
                }
            }
        }

        return (droppedItems, money);
    }

    private void PartitionDropGroups(
        IList<DropItemGroup> guaranteedDropGroups,
        IList<DropItemGroup> chanceDropGroups,
        IEnumerable<DropItemGroup> groups,
        MonsterDefinition? monster = null)
    {
        foreach (var group in groups)
        {
            if (monster is not null && !IsGroupRelevant(monster, group))
            {
                continue;
            }

            if (group.Chance >= 1.0)
            {
                guaranteedDropGroups.Add(group);
            }
            else
            {
                chanceDropGroups.Add(group);
            }
        }
    }

    private Item? GenerateItemDrop(DropItemGroup selectedGroup, ICollection<ItemDefinition> possibleItems)
    {
        var item = selectedGroup.ItemType switch
        {
            SpecialItemType.Ancient => this.GenerateRandomAncient(),
            SpecialItemType.Excellent => this.GenerateRandomExcellentItem(possibleItems: possibleItems),
            _ => this.GenerateRandomItem(possibleItems),
        };

        if (item is null)
        {
            return null;
        }

        if (item.Durability == 0)
        {
            item.Durability = item.GetMaximumDurabilityOfOnePiece();
        }

        if (selectedGroup is ItemDropItemGroup itemDropItemGroup)
        {
            item.Level = (byte)this._randomizer.NextInt(itemDropItemGroup.MinimumLevel, itemDropItemGroup.MaximumLevel + 1);
        }
        else if (selectedGroup.ItemLevel is { } itemLevel)
        {
            item.Level = itemLevel;
        }
        else
        {
            // no level defined, so it stays at 0.
        }

        item.Level = Math.Min(item.Level, item.Definition!.MaximumItemLevel);

        return item;
    }

    private void ApplyOption(Item item, ItemOptionDefinition option)
    {
        for (int i = 0; i < option.MaximumOptionsPerItem; i++)
        {
            if (this._randomizer.NextRandomBool(option.AddChance))
            {
                var remainingOptions = option.PossibleOptions.Where(possibleOption => item.ItemOptions.All(link => link.ItemOption != possibleOption));
                var newOption = remainingOptions.SelectRandom(this._randomizer);
                if (newOption is null)
                {
                    break;
                }

                var itemOptionLink = new ItemOptionLink
                {
                    ItemOption = newOption,
                    Level = newOption.LevelDependentOptions
                        .Select(ldo => ldo.Level)
                        .Concat(newOption.LevelDependentOptions.Count > 0 ? [1] : []) // For base def/dmg opts level 1 is not an ItemOptionOfLevel entry
                        .Distinct()
                        .Where(l => l <= this._maxItemOptionLevelDrop)
                        .DefaultIfEmpty(0)
                        .SelectRandom(),
                };
                item.ItemOptions.Add(itemOptionLink);
            }
        }
    }

    private Item? GenerateRandomItem(ICollection<ItemDefinition>? possibleItems)
    {
        if (possibleItems is null || possibleItems.Count == 0)
        {
            return null;
        }

        var item = new TemporaryItem
        {
            Definition = possibleItems.ElementAt(this._randomizer.NextInt(0, possibleItems.Count)),
        };

        this.ApplyRandomOptions(item);

        return item;
    }

    private void ApplyRandomAncientOption(Item item)
    {
        var ancientSet = item.Definition?.PossibleItemSetGroups
            .Where(g => g!.Options?.PossibleOptions.Any(o => object.Equals(o.OptionType, ItemOptionTypes.AncientOption)) ?? false)
            .SelectRandom(this._randomizer);
        if (ancientSet is null)
        {
            return;
        }

        var itemOfSet = ancientSet.Items.First(i => object.Equals(i.ItemDefinition, item.Definition));
        item.ItemSetGroups.Add(itemOfSet);

        // For example: +5str or +10str.
        if (itemOfSet.BonusOption is { } bonusOption)
        {
            var bonusOptionLink = new ItemOptionLink();
            bonusOptionLink.ItemOption = bonusOption;
            bonusOptionLink.Level = bonusOption.LevelDependentOptions.Select(o => o.Level).SelectRandom();
            item.ItemOptions.Add(bonusOptionLink);
        }
    }

    private void AddRandomExcOptions(Item item)
    {
        var excellentOptions = item.Definition!.PossibleItemOptions.FirstOrDefault(
            o => o.PossibleOptions.Any(p => object.Equals(p.OptionType, ItemOptionTypes.Excellent)));

        if (excellentOptions is null)
        {
            return;
        }

        var existingOptionCount = item.ItemOptions.Count(o => object.Equals(o.ItemOption?.OptionType, ItemOptionTypes.Excellent));

        for (int i = existingOptionCount; i < excellentOptions.MaximumOptionsPerItem; i++)
        {
            if (i == 0)
            {
                // The first option is always added without a chance
                var newOption = excellentOptions.PossibleOptions.SelectRandom(this._randomizer);
                if (newOption is not null)
                {
                    item.ItemOptions.Add(new ItemOptionLink { ItemOption = newOption });
                    existingOptionCount++;
                }

                continue;
            }

            if (this._randomizer.NextRandomBool(excellentOptions.AddChance))
            {
                var newOption = excellentOptions.PossibleOptions.SelectRandom(this._randomizer);
                while (item.ItemOptions.Any(o => object.Equals(o.ItemOption, newOption)))
                {
                    newOption = excellentOptions.PossibleOptions.SelectRandom(this._randomizer);
                }

                if (newOption is not null)
                {
                    item.ItemOptions.Add(new ItemOptionLink { ItemOption = newOption });
                }
            }
        }
    }

    private Item? GenerateItemDropOrMoney(MonsterDefinition monster, DropItemGroup selectedGroup, int gainedExperience, out uint? droppedMoney)
    {
        droppedMoney = null;

        if (selectedGroup.PossibleItems?.Count > 0)
        {
            return this.GenerateItemFromGroup(monster, selectedGroup);
        }

        var item = this.GenerateSpecialItem(monster, selectedGroup);
        if (item is null && selectedGroup.ItemType == SpecialItemType.Money)
        {
            droppedMoney = (uint)(gainedExperience + BaseMoneyDrop);
        }

        return item;
    }

    private Item? GenerateItemFromGroup(MonsterDefinition monster, DropItemGroup selectedGroup)
    {
        var isDropSpecificForMonster = monster.DropItemGroups.Contains(selectedGroup);
        if (isDropSpecificForMonster)
        {
            return this.GenerateItemDrop(selectedGroup, selectedGroup.PossibleItems!);
        }

        var monsterLevel = (int)monster[Stats.Level];
        var isJewel = selectedGroup.ItemType == SpecialItemType.Jewel;

        var filteredPossibleItems = selectedGroup.PossibleItems!
            .Where(it => CanDropAtMonsterLevel(it, monsterLevel)
                         && (isJewel || it.DropLevel == 0 || it.DropLevel > monsterLevel - DropLevelMaxGap))
            .ToList();

        return this.GenerateItemDrop(selectedGroup, filteredPossibleItems);
    }

    private Item? GenerateSpecialItem(MonsterDefinition monster, DropItemGroup selectedGroup)
    {
        var monsterLevel = (int)monster[Stats.Level];
        return selectedGroup.ItemType switch
        {
            SpecialItemType.Ancient => this.GenerateRandomAncient(),
            SpecialItemType.Excellent => this.GenerateRandomExcellentItem(monsterLevel),
            SpecialItemType.RandomItem => this.GenerateRandomItem(monsterLevel, false),
            SpecialItemType.SocketItem => this.GenerateRandomItem(monsterLevel, true),
            _ => null,
        };
    }

    private DropItemGroup? SelectRandomGroup(IEnumerable<DropItemGroup> groups, double totalChance)
    {
        var remainingThreshold = this._randomizer.NextDouble();
        if (totalChance > 1.0)
        {
            remainingThreshold *= totalChance;
        }

        foreach (var group in groups)
        {
            if (remainingThreshold > group.Chance)
            {
                remainingThreshold -= group.Chance;
            }
            else
            {
                return group;
            }
        }

        return null;
    }

    private IList<ItemDefinition>? GetPossibleList(int monsterLevel, bool isSocketItem = false)
    {
        if (monsterLevel is < byte.MinValue or > byte.MaxValue)
        {
            return null;
        }

        var cache = isSocketItem ? this._droppableSocketItemsPerMonsterLevel : this._droppableItemsPerMonsterLevel;
        return cache[monsterLevel]
            ??= (from it in this._droppableItems
                 where CanDropAtMonsterLevel(it, monsterLevel)
                       && (it.DropLevel > monsterLevel - DropLevelMaxGap)
                       && (!isSocketItem || it.MaximumSockets > 0)
                 select it).ToList();
    }
}
