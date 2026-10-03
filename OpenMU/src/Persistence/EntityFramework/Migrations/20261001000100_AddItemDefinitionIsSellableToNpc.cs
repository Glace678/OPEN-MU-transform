// <copyright file="20261001000100_AddItemDefinitionIsSellableToNpc.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Adds the item sellability flag. Existing rows default to <c>true</c> so current
/// behavior is unchanged; the matching update plug-in flags the non-sellable items.
/// </summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261001000100_AddItemDefinitionIsSellableToNpc")]
public class AddItemDefinitionIsSellableToNpc : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsSellableToNpc",
            schema: "config",
            table: "ItemDefinition",
            type: "boolean",
            nullable: false,
            defaultValue: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsSellableToNpc", schema: "config", table: "ItemDefinition");
    }
}
