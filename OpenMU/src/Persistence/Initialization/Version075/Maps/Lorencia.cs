// <copyright file="Lorencia.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Version075.Maps;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Skills;

/// <summary>
/// The initialization for the Lorencia map.
/// </summary>
internal class Lorencia : BaseMapInitializer
{
    /// <summary>
    /// The default number of the map.
    /// </summary>
    internal const byte Number = 0;

    /// <summary>
    /// The default name of the map.
    /// </summary>
    internal const string Name = "Lorencia";

    /// <summary>
    /// Initializes a new instance of the <see cref="Lorencia"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public Lorencia(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc/>
    protected override byte MapNumber => Number;

    /// <inheritdoc/>
    protected override string MapName => Name;

    /// <inheritdoc/>
    protected override IEnumerable<MonsterSpawnArea> CreateNpcSpawns()
    {
        yield return this.CreateMonsterSpawn(1, this.NpcDictionary[248], 6, 145, Direction.SouthEast, SpawnTrigger.Wandering);
        yield return this.CreateMonsterSpawn(2, this.NpcDictionary[240], 146, 110, Direction.SouthEast);
        yield return this.CreateMonsterSpawn(3, this.NpcDictionary[240], 147, 145, Direction.SouthWest);
        yield return this.CreateMonsterSpawn(4, this.NpcDictionary[249], 131, 88, Direction.SouthWest);
        yield return this.CreateMonsterSpawn(5, this.NpcDictionary[249], 173, 125, Direction.SouthEast);
        yield return this.CreateMonsterSpawn(6, this.NpcDictionary[249], 94, 125, Direction.NorthWest);
        yield return this.CreateMonsterSpawn(7, this.NpcDictionary[249], 94, 130, Direction.NorthWest);
        yield return this.CreateMonsterSpawn(8, this.NpcDictionary[249], 131, 148, Direction.SouthWest);
        yield return this.CreateMonsterSpawn(9, this.NpcDictionary[247], 114, 125, Direction.SouthEast);
        yield return this.CreateMonsterSpawn(10, this.NpcDictionary[250], 183, 137, Direction.South, SpawnTrigger.Wandering);
        yield return this.CreateMonsterSpawn(11, this.NpcDictionary[251], 116, 141, Direction.SouthEast);
        yield return this.CreateMonsterSpawn(12, this.NpcDictionary[253], 127, 86, Direction.South);
        yield return this.CreateMonsterSpawn(13, this.NpcDictionary[254], 118, 113, Direction.SouthEast);
        yield return this.CreateMonsterSpawn(14, this.NpcDictionary[255], 123, 135, Direction.SouthWest);
    }

    /// <inheritdoc />
    protected override IEnumerable<MonsterSpawnArea> CreateMonsterSpawns()
    {
        yield return this.CreateMonsterSpawn(120, this.NpcDictionary[000], 135, 240, 020, 088, 45);
        yield return this.CreateMonsterSpawn(121, this.NpcDictionary[003], 180, 226, 090, 244, 45);
        yield return this.CreateMonsterSpawn(122, this.NpcDictionary[002], 180, 226, 090, 244, 40);
        yield return this.CreateMonsterSpawn(123, this.NpcDictionary[002], 135, 240, 020, 088, 20);
        yield return this.CreateMonsterSpawn(124, this.NpcDictionary[006], 095, 175, 168, 244, 20);
        yield return this.CreateMonsterSpawn(125, this.NpcDictionary[014], 095, 175, 168, 244, 15);
        yield return this.CreateMonsterSpawn(126, this.NpcDictionary[001], 008, 094, 011, 244, 45);
        yield return this.CreateMonsterSpawn(127, this.NpcDictionary[004], 008, 094, 011, 244, 45);
        yield return this.CreateMonsterSpawn(128, this.NpcDictionary[007], 008, 060, 011, 080, 15);
    }

    /// <inheritdoc />
    protected override void CreateMonsters()
    {
        {
            var bullFighter = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(bullFighter);
            bullFighter.Number = 0;
            bullFighter.Designation = "Bull Fighter";
            bullFighter.MoveRange = 3;
            bullFighter.AttackRange = 1;
            bullFighter.ViewRange = 5;
            bullFighter.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
            bullFighter.AttackDelay = new TimeSpan(1600 * TimeSpan.TicksPerMillisecond);
            bullFighter.RespawnDelay = new TimeSpan(3 * TimeSpan.TicksPerSecond);
            bullFighter.Attribute = 2;
            bullFighter.NumberOfMaximumItemDrops = 1;
            var bullFighterAttributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 6 },
                { Stats.MaximumHealth, 179 },
                { Stats.MinimumPhysBaseDmg, 14 },
                { Stats.MaximumPhysBaseDmg, 18 },
                { Stats.DefenseBase, 15 },
                { Stats.AttackRatePvm, 123 },
                { Stats.DefenseRatePvm, 123 },
            };
            bullFighter.AddAttributes(bullFighterAttributes, this.Context, this.GameConfiguration);
        }

        {
            var hound = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(hound);
            hound.Number = 1;
            hound.Designation = "Hound";
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 9 },
                { Stats.MaximumHealth, 231 },
                { Stats.MinimumPhysBaseDmg, 17 },
                { Stats.MaximumPhysBaseDmg, 23 },
                { Stats.DefenseBase, 18 },
                { Stats.AttackRatePvm, 139 },
                { Stats.DefenseRatePvm, 139 },
            };
            hound.AddAttributes(attributes, this.Context, this.GameConfiguration);
            hound.MoveRange = 3;
            hound.AttackRange = 1;
            hound.ViewRange = 5;
            hound.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
            hound.AttackDelay = new TimeSpan(1600 * TimeSpan.TicksPerMillisecond);
            hound.RespawnDelay = new TimeSpan(10 * TimeSpan.TicksPerSecond);
            hound.Attribute = 2;
            hound.NumberOfMaximumItemDrops = 1;
        }

        {
            var budgeDragon = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(budgeDragon);
            budgeDragon.Number = 2;
            budgeDragon.Designation = "Budge Dragon";
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 4 },
                { Stats.MaximumHealth, 144 },
                { Stats.MinimumPhysBaseDmg, 11 },
                { Stats.MaximumPhysBaseDmg, 15 },
                { Stats.DefenseBase, 13 },
                { Stats.AttackRatePvm, 113 },
                { Stats.DefenseRatePvm, 113 },
            };
            budgeDragon.AddAttributes(attributes, this.Context, this.GameConfiguration);
            budgeDragon.MoveRange = 3;
            budgeDragon.AttackRange = 1;
            budgeDragon.ViewRange = 4;
            budgeDragon.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
            budgeDragon.AttackDelay = new TimeSpan(2000 * TimeSpan.TicksPerMillisecond);
            budgeDragon.RespawnDelay = new TimeSpan(3 * TimeSpan.TicksPerSecond);
            budgeDragon.Attribute = 2;
            budgeDragon.NumberOfMaximumItemDrops = 1;
        }

        {
            var spider = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(spider);
            spider.Number = 3;
            spider.Designation = "Spider";
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 2 },
                { Stats.MaximumHealth, 110 },
                { Stats.MinimumPhysBaseDmg, 9 },
                { Stats.MaximumPhysBaseDmg, 12 },
                { Stats.DefenseBase, 11 },
                { Stats.AttackRatePvm, 103 },
                { Stats.DefenseRatePvm, 103 },
            };
            spider.AddAttributes(attributes, this.Context, this.GameConfiguration);
            spider.MoveRange = 2;
            spider.AttackRange = 1;
            spider.ViewRange = 5;
            spider.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
            spider.AttackDelay = new TimeSpan(1800 * TimeSpan.TicksPerMillisecond);
            spider.RespawnDelay = new TimeSpan(10 * TimeSpan.TicksPerSecond);
            spider.Attribute = 2;
            spider.NumberOfMaximumItemDrops = 1;
        }

        {
            var eliteBullFighter = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(eliteBullFighter);
            eliteBullFighter.Number = 4;
            eliteBullFighter.Designation = "Elite Bull Fighter";
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 12 },
                { Stats.MaximumHealth, 285 },
                { Stats.MinimumPhysBaseDmg, 20 },
                { Stats.MaximumPhysBaseDmg, 28 },
                { Stats.DefenseBase, 22 },
                { Stats.AttackRatePvm, 154 },
                { Stats.DefenseRatePvm, 154 },
            };
            eliteBullFighter.AddAttributes(attributes, this.Context, this.GameConfiguration);
            eliteBullFighter.MoveRange = 3;
            eliteBullFighter.AttackRange = 1;
            eliteBullFighter.ViewRange = 4;
            eliteBullFighter.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
            eliteBullFighter.AttackDelay = new TimeSpan(1400 * TimeSpan.TicksPerMillisecond);
            eliteBullFighter.RespawnDelay = new TimeSpan(10 * TimeSpan.TicksPerSecond);
            eliteBullFighter.Attribute = 2;
            eliteBullFighter.NumberOfMaximumItemDrops = 1;
        }

        {
            var lich = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(lich);
            lich.Number = 6;
            lich.Designation = "Lich";
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 14 },
                { Stats.MaximumHealth, 320 },
                { Stats.MinimumPhysBaseDmg, 23 },
                { Stats.MaximumPhysBaseDmg, 31 },
                { Stats.DefenseBase, 24 },
                { Stats.AttackRatePvm, 164 },
                { Stats.DefenseRatePvm, 164 },
                { Stats.FireResistance, 1f / 255 },
            };
            lich.AddAttributes(attributes, this.Context, this.GameConfiguration);
            lich.MoveRange = 3;
            lich.AttackRange = 4;
            lich.ViewRange = 7;
            lich.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
            lich.AttackDelay = new TimeSpan(2000 * TimeSpan.TicksPerMillisecond);
            lich.RespawnDelay = new TimeSpan(10 * TimeSpan.TicksPerSecond);
            lich.Attribute = 2;
            lich.NumberOfMaximumItemDrops = 1;
            lich.AttackSkill = this.GameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)SkillNumber.Meteorite);
        }

        {
            var giant = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(giant);
            giant.Number = 7;
            giant.Designation = "Giant";
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 17 },
                { Stats.MaximumHealth, 374 },
                { Stats.MinimumPhysBaseDmg, 26 },
                { Stats.MaximumPhysBaseDmg, 35 },
                { Stats.DefenseBase, 27 },
                { Stats.AttackRatePvm, 180 },
                { Stats.DefenseRatePvm, 180 },
            };
            giant.AddAttributes(attributes, this.Context, this.GameConfiguration);
            giant.MoveRange = 2;
            giant.AttackRange = 2;
            giant.ViewRange = 3;
            giant.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
            giant.AttackDelay = new TimeSpan(2200 * TimeSpan.TicksPerMillisecond);
            giant.RespawnDelay = new TimeSpan(10 * TimeSpan.TicksPerSecond);
            giant.Attribute = 2;
            giant.NumberOfMaximumItemDrops = 1;
        }

        {
            var skeleton = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(skeleton);
            skeleton.Number = 14;
            skeleton.Designation = "Skeleton Warrior";
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 19 },
                { Stats.MaximumHealth, 411 },
                { Stats.MinimumPhysBaseDmg, 28 },
                { Stats.MaximumPhysBaseDmg, 38 },
                { Stats.DefenseBase, 30 },
                { Stats.AttackRatePvm, 190 },
                { Stats.DefenseRatePvm, 190 },
            };
            skeleton.AddAttributes(attributes, this.Context, this.GameConfiguration);
            skeleton.MoveRange = 2;
            skeleton.AttackRange = 1;
            skeleton.ViewRange = 4;
            skeleton.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
            skeleton.AttackDelay = new TimeSpan(1400 * TimeSpan.TicksPerMillisecond);
            skeleton.RespawnDelay = new TimeSpan(10 * TimeSpan.TicksPerSecond);
            skeleton.Attribute = 2;
            skeleton.NumberOfMaximumItemDrops = 1;
        }
    }
}