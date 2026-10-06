// <copyright file="EarlyGameCombatWinnableTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions.Character;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Full-data combat test proving a newly created character can kill the weakest
/// starting monster using the real Season 6 configuration and the real damage path.
/// </summary>
[TestFixture]
public class EarlyGameCombatWinnableTests
{
    /// <summary>A freshly created Dark Knight must kill a Spider before dying.</summary>
    [Test]
    public async ValueTask CreatedDarkKnightKillsSpiderAsync()
    {
        var (provider, configuration, context) = await CreateContextAsync().ConfigureAwait(false);
        await using var ownedContext = context;

        var player = await CreatePlayerAsync(context, provider, configuration, stageNumber: 4, level: 1).ConfigureAwait(false);
        await using var ownedPlayer = player;

        var map = await context.GetMapAsync(0).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Missing Lorencia test map.");
        var spider = CreateSpider(map, configuration, context);

        var result = await FightToDeathAsync(player, spider).ConfigureAwait(false);
        await File.WriteAllLinesAsync(
            Path.Combine(Path.GetTempPath(), "openmu_combat_log.txt"), result.Log).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(result.PlayerWins, Is.True, "A new character must be able to kill a Spider.");
            Assert.That(spider.Health, Is.LessThanOrEqualTo(0));
            Assert.That(player.Attributes![Stats.CurrentHealth], Is.GreaterThan(0));
        });
    }

    private static Monster CreateSpider(GameMap map, GameConfiguration configuration, GameContext context)
    {
        var spiderDefinition = configuration.Monsters.First(m => m.Number == 3);
        var spawn = new Persistence.BasicModel.MonsterSpawnArea
        {
            MonsterDefinition = spiderDefinition,
            GameMap = map.Definition,
            X1 = 185,
            Y1 = 120,
            X2 = 185,
            Y2 = 120,
            Quantity = 1,
        };
        var monster = new Monster(
            spawn,
            spiderDefinition,
            map,
            NullDropGenerator.Instance,
            new Mock<INpcIntelligence>().Object,
            context.PlugInManager,
            context.PathFinderPool,
            balanceConfiguration: configuration);
        monster.Initialize();
        return monster;
    }

    private static async ValueTask<(bool PlayerWins, List<string> Log)> FightToDeathAsync(Player player, Monster monster)
    {
        const int maximumRounds = 100;
        var log = new List<string>();

        for (var round = 0; round < maximumRounds; round++)
        {
            var playerHit = await player.CalculateDamageAsync(monster, null, false).ConfigureAwait(false);
            DealDamage(monster, playerHit);
            log.Add($"p->m dmg={playerHit.HealthDamage} mHP={monster.Health}");
            if (monster.Health <= 0)
            {
                return (true, log);
            }

            var monsterHit = await monster.CalculateDamageAsync(player, null, false).ConfigureAwait(false);
            DealDamage(player, monsterHit);
            log.Add($"m->p dmg={monsterHit.HealthDamage} pHP={player.Attributes![Stats.CurrentHealth]}");
            if (player.Attributes[Stats.CurrentHealth] <= 0)
            {
                return (false, log);
            }
        }

        return (false, log);
    }

    private static void DealDamage(IAttackable target, HitInfo hit)
    {
        var health = target.Attributes[Stats.CurrentHealth] - hit.HealthDamage;
        target.Attributes[Stats.CurrentHealth] = Math.Max(0, health);
    }

    private static async Task<Player> CreatePlayerAsync(GameContext context, InMemoryPersistenceContextProvider provider,
        GameConfiguration configuration, int stageNumber, int level)
    {
        var player = new CombatPlayer(context)
        {
            Account = provider.CreateNewContext(configuration).CreateNew<Account>(),
        };
        await player.PlayerState.TryAdvanceToAsync(PlayerState.LoginScreen).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.CharacterSelection).ConfigureAwait(false);

        var darkKnightClass = configuration.CharacterClasses.First(c => c.Number == stageNumber);

        var character = player.PersistenceContext.CreateNew<Character>();
        character.CharacterClass = darkKnightClass;
        character.Name = "CombatTest";
        character.CurrentMap = darkKnightClass.HomeMap;
        character.PositionX = 130;
        character.PositionY = 120;
        foreach (var stat in darkKnightClass.StatAttributes)
        {
            character.Attributes.Add(player.PersistenceContext.CreateNew<StatAttribute>(stat.Attribute, stat.BaseValue));
        }

        character.Inventory = player.PersistenceContext.CreateNew<ItemStorage>();
        player.Account.Characters.Add(character);
        context.PlugInManager.GetPlugInPoint<ICharacterCreatedPlugIn>()?.CharacterCreated(player, character);

        await player.SetSelectedCharacterAsync(character).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        return player;
    }

    private static async Task<(InMemoryPersistenceContextProvider Provider, GameConfiguration Configuration, GameContext Context)> CreateContextAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var read = provider.CreateNewConfigurationContext();
        var configuration = (await read.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        configuration.RecoveryInterval = int.MaxValue;
        using (var write = provider.CreateNewContext(configuration))
        {
            new BalanceV1Initializer(write, configuration, BalanceV1.ExperienceProfile.Standard).Initialize();
        }

        var pluginTypes = typeof(DataInitialization).Assembly.GetTypes()
            .Where(type => typeof(ICharacterCreatedPlugIn).IsAssignableFrom(type) && !type.IsAbstract)
            .Select(type => type.GUID).ToHashSet();
        var manager = new PlugInManager(
            configuration.PlugInConfigurations.Where(entry => pluginTypes.Contains(entry.TypeId)).ToList(),
            NullLoggerFactory.Instance, null, null);

        var factory = new MapInitializer(configuration, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var initializer = new Mock<IMapInitializer>();
        initializer.Setup(value => value.CreateGameMap(It.IsAny<ushort>())).Returns((ushort number) => factory.CreateGameMap(number));
        initializer.Setup(value => value.InitializeStateAsync(It.IsAny<GameMap>())).Returns(ValueTask.CompletedTask);
        var context = new GameContext(configuration, provider, initializer.Object, NullLoggerFactory.Instance,
            manager, NullDropGenerator.Instance, new ConfigurationChangeMediator());
        factory.PlugInManager = context.PlugInManager;
        factory.PathFinderPool = context.PathFinderPool;
        return (provider, configuration, context);
    }

    private sealed class CombatPlayer(IGameContext context) : Player(context)
    {
        protected override ICustomPlugInContainer<IViewPlugIn> CreateViewPlugInContainer() => new MockViewPlugInContainer();
    }
}
