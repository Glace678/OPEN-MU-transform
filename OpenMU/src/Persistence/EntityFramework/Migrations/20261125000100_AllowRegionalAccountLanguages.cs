// <copyright file="20261125000100_AllowRegionalAccountLanguages.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Allows storing regional language codes accepted during account registration.
/// </summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261125000100_AllowRegionalAccountLanguages")]
public class AllowRegionalAccountLanguages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AlterColumn<string>(
            name: "LanguageIsoCode",
            schema: "data",
            table: "Account",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "en",
            oldClrType: typeof(string),
            oldType: "character varying(3)",
            oldMaxLength: 3,
            oldDefaultValue: "en");

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE data."Account"
            SET "LanguageIsoCode" = CASE WHEN "LanguageIsoCode" LIKE 'zh-%' THEN 'zh' ELSE 'en' END
            WHERE length("LanguageIsoCode") > 3;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "LanguageIsoCode",
            schema: "data",
            table: "Account",
            type: "character varying(3)",
            maxLength: 3,
            nullable: false,
            defaultValue: "en",
            oldClrType: typeof(string),
            oldType: "character varying(16)",
            oldMaxLength: 16,
            oldDefaultValue: "en");
    }
}
