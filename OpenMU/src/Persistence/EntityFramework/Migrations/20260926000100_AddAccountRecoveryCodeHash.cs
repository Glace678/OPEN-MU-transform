// <copyright file="20260926000100_AddAccountRecoveryCodeHash.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>Adds opt-in account recovery storage without backfilling existing accounts.</summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20260926000100_AddAccountRecoveryCodeHash")]
public class AddAccountRecoveryCodeHash : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RecoveryCodeHash",
            schema: "data",
            table: "Account",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "RecoveryCodeHash", schema: "data", table: "Account");
    }
}
