// <copyright file="20260905000100_AddSoloCashShopData.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Adds the local solo shop ledger without modifying existing balances or items.
/// </summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20260905000100_AddSoloCashShopData")]
public class AddSoloCashShopData : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(
            name: "SoloCashShopData",
            schema: "data",
            table: "Account",
            type: "text",
            nullable: false,
            defaultValue: string.Empty);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "SoloCashShopData", schema: "data", table: "Account");
}
