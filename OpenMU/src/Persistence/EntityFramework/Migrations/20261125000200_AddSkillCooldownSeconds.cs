// <copyright file="20261125000200_AddSkillCooldownSeconds.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Persists the cooldown specified by the skill initializer.
/// </summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261125000200_AddSkillCooldownSeconds")]
public class AddSkillCooldownSeconds : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "CooldownSeconds",
            schema: "config",
            table: "Skill",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        // The skill numbers and durations match the Season Six initializer.
        migrationBuilder.Sql("""
            UPDATE config."Skill" AS skill
            SET "CooldownSeconds" = defaults.seconds
            FROM (VALUES (67, 'Stun', 240), (68, 'Cancel Stun', 120), (69, 'Swell Mana', 240),
                         (70, 'Invisibility', 300), (71, 'Cancel Invisibility', 120),
                         (72, 'Abolish Magic', 480), (210, 'Spell of Protection', 300),
                         (211, 'Spell of Restriction', 300), (212, 'Spell of Pursuit', 600),
                         (213, 'Shield-Burn', 300)) AS defaults(number, name, seconds)
            WHERE skill."Number" = defaults.number AND skill."Name" = defaults.name;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "CooldownSeconds", schema: "config", table: "Skill");
}
