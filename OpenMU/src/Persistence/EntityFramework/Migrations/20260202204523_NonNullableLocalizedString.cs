// <copyright file="20260202204523_NonNullableLocalizedString.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class NonNullableLocalizedString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill existing NULL rows before enforcing NOT NULL; otherwise the
            // column alteration fails on databases that already contain NULL values.
            migrationBuilder.Sql("""UPDATE config."MiniGameSpawnWave" SET "Message" = '' WHERE "Message" IS NULL;""");
            migrationBuilder.Sql("""UPDATE config."MiniGameSpawnWave" SET "Description" = '' WHERE "Description" IS NULL;""");
            migrationBuilder.Sql("""UPDATE config."MiniGameChangeEvent" SET "Message" = '' WHERE "Message" IS NULL;""");
            migrationBuilder.Sql("""UPDATE config."MiniGameChangeEvent" SET "Description" = '' WHERE "Description" IS NULL;""");
            migrationBuilder.Sql("""UPDATE config."ConfigurationUpdate" SET "Name" = '' WHERE "Name" IS NULL;""");
            migrationBuilder.Sql("""UPDATE config."ConfigurationUpdate" SET "Description" = '' WHERE "Description" IS NULL;""");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                schema: "config",
                table: "MiniGameSpawnWave",
                type: "text",
                nullable: false,
                defaultValue: string.Empty,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "config",
                table: "MiniGameSpawnWave",
                type: "text",
                nullable: false,
                defaultValue: string.Empty,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                schema: "config",
                table: "MiniGameChangeEvent",
                type: "text",
                nullable: false,
                defaultValue: string.Empty,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "config",
                table: "MiniGameChangeEvent",
                type: "text",
                nullable: false,
                defaultValue: string.Empty,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "config",
                table: "ConfigurationUpdate",
                type: "text",
                nullable: false,
                defaultValue: string.Empty,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "config",
                table: "ConfigurationUpdate",
                type: "text",
                nullable: false,
                defaultValue: string.Empty,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Message",
                schema: "config",
                table: "MiniGameSpawnWave",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "config",
                table: "MiniGameSpawnWave",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                schema: "config",
                table: "MiniGameChangeEvent",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "config",
                table: "MiniGameChangeEvent",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "config",
                table: "ConfigurationUpdate",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "config",
                table: "ConfigurationUpdate",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
