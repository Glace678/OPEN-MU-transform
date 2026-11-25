// <copyright file="20261125000300_AddCaseInsensitiveLoginNameIndex.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Enforces the documented case-insensitive uniqueness of the account login name.
/// </summary>
/// <remarks>
/// The model comment already promised a case-insensitive unique login name, but no migration
/// created the functional index. A plain unique index on "LoginName" does not prevent "Admin" and
/// "admin" from existing twice. Existing databases are checked first, so a conflict is reported with
/// the affected names instead of failing with an opaque duplicate key error.
/// </remarks>
[DbContext(typeof(EntityDataContext))]
[Migration("20261125000300_AddCaseInsensitiveLoginNameIndex")]
public class AddCaseInsensitiveLoginNameIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            DECLARE
                conflicts text;
            BEGIN
                SELECT string_agg(format('%s (%sx)', normalized_name, account_count),
                                  ', ' ORDER BY normalized_name)
                INTO conflicts
                FROM (
                    SELECT lower("LoginName") AS normalized_name,
                           count(*) AS account_count
                    FROM data."Account"
                    GROUP BY lower("LoginName")
                    HAVING count(*) > 1
                ) AS duplicate_names;

                IF conflicts IS NOT NULL THEN
                    RAISE EXCEPTION 'Cannot create the case-insensitive login name index: duplicate accounts exist for %', conflicts;
                END IF;
            END $$;
            """);

        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Account_LoginName_Lower"
            ON data."Account" (lower("LoginName"));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP INDEX IF EXISTS data."IX_Account_LoginName_Lower";""");
    }
}
