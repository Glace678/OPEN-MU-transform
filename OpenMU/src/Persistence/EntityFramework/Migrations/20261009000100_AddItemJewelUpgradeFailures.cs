// <copyright file="20261009000100_AddItemJewelUpgradeFailures.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Adds the persisted per-item failed jewel upgrade counter which backs the
/// balance-v1 pity guarantee (UpgradeStep.PityAttempts).
/// </summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261009000100_AddItemJewelUpgradeFailures")]
public class AddItemJewelUpgradeFailures : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<int>(
            name: "JewelUpgradeFailures",
            schema: "data",
            table: "Item",
            type: "integer",
            nullable: false,
            defaultValue: 0);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "JewelUpgradeFailures", schema: "data", table: "Item");
}
