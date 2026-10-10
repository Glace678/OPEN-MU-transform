// <copyright file="Noria.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Version075.Maps;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// The initialization for the Noria map.
/// </summary>
internal class Noria : BaseMapInitializer
{
    /// <summary>
    /// The default number of the map.
    /// </summary>
    internal const byte Number = 3;

    /// <summary>
    /// The default name of the map.
    /// </summary>
    internal const string Name = "Noria";

    /// <summary>
    /// Initializes a new instance of the <see cref="Noria"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public Noria(IContext context, GameConfiguration gameConfiguration)
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
        yield return this.CreateMonsterSpawn(1, this.NpcDictionary[253], 169, 109, Direction.SouthEast);
        yield return this.CreateMonsterSpawn(2, this.NpcDictionary[253], 193, 110, Direction.South);
        yield return this.CreateMonsterSpawn(3, this.NpcDictionary[242], 173, 125, Direction.SouthWest);
        yield return this.CreateMonsterSpawn(4, this.NpcDictionary[243], 195, 124, Direction.South);
        yield return this.CreateMonsterSpawn(5, this.NpcDictionary[240], 172, 96, Direction.SouthEast);
        yield return this.CreateMonsterSpawn(6, this.NpcDictionary[238], 180, 103, Direction.SouthWest);
    }

    /// <inheritdoc/>
    protected override IEnumerable<MonsterSpawnArea> CreateMonsterSpawns()
    {
        yield return this.CreateMonsterSpawn(21, this.NpcDictionary[26], 128, 251, 0, 128, 155);
        yield return this.CreateMonsterSpawn(22, this.NpcDictionary[27], 128, 251, 0, 128, 125);
        yield return this.CreateMonsterSpawn(23, this.NpcDictionary[28], 0, 128, 0, 128, 125);
        yield return this.CreateMonsterSpawn(24, this.NpcDictionary[29], 0, 128, 0, 128, 125);
        yield return this.CreateMonsterSpawn(25, this.NpcDictionary[30], 0, 251, 128, 245, 125);
        yield return this.CreateMonsterSpawn(26, this.NpcDictionary[31], 0, 251, 128, 245, 125);
        yield return this.CreateMonsterSpawn(27, this.NpcDictionary[32], 128, 251, 128, 245, 100);
        yield return this.CreateMonsterSpawn(28, this.NpcDictionary[33], 0, 128, 128, 245, 125);
    }

    /// <inheritdoc/>
    protected override void CreateMonsters()
    {
        {
            var monster = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(monster);
            monster.Number = 26;
            monster.Designation = "Goblin";
            monster.MoveRange = 2;
            monster.AttackRange = 1;
            monster.ViewRange = 4;
            monster.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
            monster.AttackDelay = new TimeSpan(1800 * TimeSpan.TicksPerMillisecond);
            monster.RespawnDelay = new TimeSpan(10 * TimeSpan.TicksPerSecond);
            monster.Attribute = 2;
            monster.NumberOfMaximumItemDrops = 1;
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 3 },
                { Stats.MaximumHealth, 110 },
                { Stats.MinimumPhysBaseDmg, 9 },
                { Stats.MaximumPhysBaseDmg, 12 },
                { Stats.DefenseBase, 11 },
                { Stats.AttackRatePvm, 103 },
                { Stats.DefenseRatePvm, 103 },
                { Stats.WindResistance, 0f / 255 },
                { Stats.PoisonResistance, 0f / 255 },
                { Stats.IceResistance, 0f / 255 },
                { Stats.WaterResistance, 0f / 255 },
                { Stats.FireResistance, 0f / 255 },
            };
            monster.AddAttributes(attributes, this.Context, this.GameConfiguration);
            monster.SetGuid(monster.Number);
        }

        {
            var monster = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(monster);
            monster.Number = 27;
            monster.Designation = "Chain Scorpion";
            monster.MoveRange = 3;
            monster.AttackRange = 1;
            monster.ViewRange = 4;
            monster.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
            monster.AttackDelay = new TimeSpan(1800 * TimeSpan.TicksPerMillisecond);
            monster.RespawnDelay = new TimeSpan(10 * TimeSpan.TicksPerSecond);
            monster.Attribute = 2;
            monster.NumberOfMaximumItemDrops = 1;
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 5 },
                { Stats.MaximumHealth, 149 },
                { Stats.MinimumPhysBaseDmg, 12 },
                { Stats.MaximumPhysBaseDmg, 16 },
                { Stats.DefenseBase, 13 },
                { Stats.AttackRatePvm, 115 },
                { Stats.DefenseRatePvm, 115 },
            };

            monster.AddAttributes(attributes, this.Context, this.GameConfiguration);
            monster.SetGuid(monster.Number);
        }

        {
            var monster = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(monster);
            monster.Number = 28;
            monster.Designation = "Beetle Monster";
            monster.MoveRange = 3;
            monster.AttackRange = 1;
            monster.ViewRange = 5;
            monster.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
            monster.AttackDelay = new TimeSpan(1600 * TimeSpan.TicksPerMillisecond);
            monster.RespawnDelay = new TimeSpan(10 * TimeSpan.TicksPerSecond);
            monster.Attribute = 2;
            monster.NumberOfMaximumItemDrops = 1;
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 10 },
                { Stats.MaximumHealth, 248 },
                { Stats.MinimumPhysBaseDmg, 18 },
                { Stats.MaximumPhysBaseDmg, 24 },
                { Stats.DefenseBase, 19 },
                { Stats.AttackRatePvm, 144 },
                { Stats.DefenseRatePvm, 144 },
            };
            monster.AddAttributes(attributes, this.Context, this.GameConfiguration);
            monster.SetGuid(monster.Number);
        }

        {
            var monster = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(monster);
            monster.Number = 29;
            monster.Designation = "Hunter";
            monster.MoveRange = 3;
            monster.AttackRange = 4;
            monster.ViewRange = 4;
            monster.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
            monster.AttackDelay = new TimeSpan(1600 * TimeSpan.TicksPerMillisecond);
            monster.RespawnDelay = new TimeSpan(10 * TimeSpan.TicksPerSecond);
            monster.Attribute = 2;
            monster.NumberOfMaximumItemDrops = 1;
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 13 },
                { Stats.MaximumHealth, 308 },
                { Stats.MinimumPhysBaseDmg, 22 },
                { Stats.MaximumPhysBaseDmg, 30 },
                { Stats.DefenseBase, 23 },
                { Stats.AttackRatePvm, 161 },
                { Stats.DefenseRatePvm, 161 },
            };

            monster.AddAttributes(attributes, this.Context, this.GameConfiguration);
            monster.SetGuid(monster.Number);
        }

        {
            var monster = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(monster);
            monster.Number = 30;
            monster.Designation = "Forest Monster";
            monster.MoveRange = 3;
            monster.AttackRange = 1;
            monster.ViewRange = 4;
            monster.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
            monster.AttackDelay = new TimeSpan(1600 * TimeSpan.TicksPerMillisecond);
            monster.RespawnDelay = new TimeSpan(10 * TimeSpan.TicksPerSecond);
            monster.Attribute = 2;
            monster.NumberOfMaximumItemDrops = 1;
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 15 },
                { Stats.MaximumHealth, 349 },
                { Stats.MinimumPhysBaseDmg, 25 },
                { Stats.MaximumPhysBaseDmg, 33 },
                { Stats.DefenseBase, 26 },
                { Stats.AttackRatePvm, 173 },
                { Stats.DefenseRatePvm, 173 },
            };

            monster.AddAttributes(attributes, this.Context, this.GameConfiguration);
            monster.SetGuid(monster.Number);
        }

        {
            var monster = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(monster);
            monster.Number = 31;
            monster.Designation = "Agon";
            monster.MoveRange = 2;
            monster.AttackRange = 1;
            monster.ViewRange = 4;
            monster.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
            monster.AttackDelay = new TimeSpan(1400 * TimeSpan.TicksPerMillisecond);
            monster.RespawnDelay = new TimeSpan(10 * TimeSpan.TicksPerSecond);
            monster.Attribute = 2;
            monster.NumberOfMaximumItemDrops = 1;
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 16 },
                { Stats.MaximumHealth, 369 },
                { Stats.MinimumPhysBaseDmg, 26 },
                { Stats.MaximumPhysBaseDmg, 35 },
                { Stats.DefenseBase, 27 },
                { Stats.AttackRatePvm, 178 },
                { Stats.DefenseRatePvm, 178 },
            };

            monster.AddAttributes(attributes, this.Context, this.GameConfiguration);
            monster.SetGuid(monster.Number);
        }

        {
            var monster = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(monster);
            monster.Number = 32;
            monster.Designation = "Stone Golem";
            monster.MoveRange = 2;
            monster.AttackRange = 2;
            monster.ViewRange = 3;
            monster.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
            monster.AttackDelay = new TimeSpan(2200 * TimeSpan.TicksPerMillisecond);
            monster.RespawnDelay = new TimeSpan(10 * TimeSpan.TicksPerSecond);
            monster.Attribute = 2;
            monster.NumberOfMaximumItemDrops = 1;
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 18 },
                { Stats.MaximumHealth, 411 },
                { Stats.MinimumPhysBaseDmg, 28 },
                { Stats.MaximumPhysBaseDmg, 38 },
                { Stats.DefenseBase, 30 },
                { Stats.AttackRatePvm, 190 },
                { Stats.DefenseRatePvm, 190 },
            };
            monster.AddAttributes(attributes, this.Context, this.GameConfiguration);
            monster.SetGuid(monster.Number);
        }

        {
            var monster = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(monster);
            monster.Number = 33;
            monster.Designation = "Elite Goblin";
            monster.MoveRange = 3;
            monster.AttackRange = 1;
            monster.ViewRange = 5;
            monster.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
            monster.AttackDelay = new TimeSpan(1600 * TimeSpan.TicksPerMillisecond);
            monster.RespawnDelay = new TimeSpan(10 * TimeSpan.TicksPerSecond);
            monster.Attribute = 2;
            monster.NumberOfMaximumItemDrops = 1;
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 8 },
                { Stats.MaximumHealth, 208 },
                { Stats.MinimumPhysBaseDmg, 16 },
                { Stats.MaximumPhysBaseDmg, 21 },
                { Stats.DefenseBase, 17 },
                { Stats.AttackRatePvm, 132 },
                { Stats.DefenseRatePvm, 132 },
            };

            monster.AddAttributes(attributes, this.Context, this.GameConfiguration);
            monster.SetGuid(monster.Number);
        }
    }
}