// <copyright file="20261125000400_GameConfigurationCastleSiegeSetNull.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Changes the optional GameConfiguration -> CastleSiegeConfiguration reference from Cascade to
/// SetNull. With Cascade, deleting the castle siege sub configuration deleted the root game
/// configuration (and everything attached to it) because the foreign key is owned by
/// GameConfiguration.
/// </summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261125000400_GameConfigurationCastleSiegeSetNull")]
public class GameConfigurationCastleSiegeSetNull : Migration
{
    private const string ForeignKeyName = "FK_GameConfiguration_CastleSiegeConfiguration_CastleSiegeConfi~";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: ForeignKeyName,
            schema: "config",
            table: "GameConfiguration");

        migrationBuilder.AddForeignKey(
            name: ForeignKeyName,
            schema: "config",
            table: "GameConfiguration",
            column: "CastleSiegeConfigurationId",
            principalSchema: "config",
            principalTable: "CastleSiegeConfiguration",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: ForeignKeyName,
            schema: "config",
            table: "GameConfiguration");

        migrationBuilder.AddForeignKey(
            name: ForeignKeyName,
            schema: "config",
            table: "GameConfiguration",
            column: "CastleSiegeConfigurationId",
            principalSchema: "config",
            principalTable: "CastleSiegeConfiguration",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
