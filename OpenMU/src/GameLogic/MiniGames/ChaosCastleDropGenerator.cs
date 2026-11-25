// <copyright file="ChaosCastleDropGenerator.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames;

using System.Collections.Immutable;
using System.Threading;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// An <see cref="IDropGenerator"/> especially for Chaos Castle.
/// It will drop a predefined amount of jewels during the whole event,
/// and nothing more.
/// </summary>
public class ChaosCastleDropGenerator : IDropGenerator
{
    private static readonly IImmutableList<(int Blesses, int Souls)> MonsterJewelDropsPerLevel = new List<(int Blesses, int Souls)>
    {
        new(0, 0), // Dummy
        new(0, 2), // Chaos Castle 1
        new(1, 1), // Chaos Castle 2
        new(1, 2), // Chaos Castle 3
        new(1, 2), // Chaos Castle 4
        new(2, 1), // Chaos Castle 5
        new(2, 2), // Chaos Castle 6
        new(2, 3), // Chaos Castle 7
    }.ToImmutableList();

    private readonly Dictionary<int, ItemDefinition> _monsterDrops = new();
    private readonly IGameContext _gameContext;
    private int _killedMonsters;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChaosCastleDropGenerator"/> class.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="context">The context.</param>
    /// <param name="spawnedMonstersCount">The spawned monsters count.</param>
    public ChaosCastleDropGenerator(IGameContext gameContext, ChaosCastleContext context, int spawnedMonstersCount)
    {
        this._gameContext = gameContext;

        // TODO: This should rather be configurable...
        var blessDefinition = this._gameContext.Configuration.Items.First(item => item is { Group: 14, Number: 13 });
        var soulDefinition = this._gameContext.Configuration.Items.First(item => item is { Group: 14, Number: 14 });
        var drops = MonsterJewelDropsPerLevel[context.Definition.GameLevel];

        var dropDefinitions = new List<ItemDefinition>(drops.Blesses + drops.Souls);
        for (var i = 0; i < drops.Blesses; i++)
        {
            dropDefinitions.Add(blessDefinition);
        }

        for (var i = 0; i < drops.Souls; i++)
        {
            dropDefinitions.Add(soulDefinition);
        }

        // Every jewel has to be assigned to a distinct kill. The number of kills is limited by the
        // monsters which are actually spawned, so more pre-allocated jewels than kills can never be
        // distributed - the assignment loop below must not wait for an impossible free kill.
        var killCount = Math.Max(0, spawnedMonstersCount);
        if (dropDefinitions.Count > killCount)
        {
            dropDefinitions.RemoveRange(killCount, dropDefinitions.Count - killCount);
        }

        var availableKills = new List<int>(killCount);
        for (var kill = 1; kill <= killCount; kill++)
        {
            availableKills.Add(kill);
        }

        // Shuffle the kills and take the first ones, so every jewel still gets a random kill, but a
        // jewel count which is too high for the monster count can no longer spin forever.
        for (var i = availableKills.Count - 1; i > 0; i--)
        {
            var swapIndex = Rand.NextInt(0, i + 1);
            (availableKills[i], availableKills[swapIndex]) = (availableKills[swapIndex], availableKills[i]);
        }

        for (var i = 0; i < dropDefinitions.Count; i++)
        {
            this._monsterDrops[availableKills[i]] = dropDefinitions[i];
        }
    }

    /// <inheritdoc />
    public async ValueTask<(IEnumerable<Item> Items, uint? Money)> GenerateItemDropsAsync(MonsterDefinition monster, int gainedExperience, Player player)
    {
        var killCount = Interlocked.Increment(ref this._killedMonsters);
        if (this._monsterDrops.TryGetValue(killCount, out var drop))
        {
            var item = new TemporaryItem
            {
                Definition = drop,
                Durability = 1,
            };

            return (item.GetAsEnumerable(), null);
        }

        return (Enumerable.Empty<Item>(), null);
    }

    /// <inheritdoc />
    public Item? GenerateItemDrop(DropItemGroup group)
    {
        return this._gameContext.DropGenerator.GenerateItemDrop(group);
    }

    /// <inheritdoc />
    public (Item? Item, uint? Money, ItemDropEffect DropEffect) GenerateItemDrop(IEnumerable<DropItemGroup> groups)
    {
        return this._gameContext.DropGenerator.GenerateItemDrop(groups);
    }
}