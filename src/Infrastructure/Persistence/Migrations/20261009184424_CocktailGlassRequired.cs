using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JiggerJot.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// JJ-043 (#182): a cocktail's glass is required. Runs after <c>ReseedCuratedCatalog</c>, which has
    /// already removed the shared recipes the old catalog shipped without one.
    /// <para>
    /// <b>No default value</b>, unlike what EF scaffolds: a default would hand every recipe without a glass
    /// the empty id, which names no glass at all. A household recipe without one cannot be given a glass by
    /// a migration — that would be the guess JJ-034 forbids — so <see cref="CheckSql"/> stops here with a
    /// message saying which rows to fix, rather than leave the ALTER to fail with a bare "contains null
    /// values". Staging had none when this was written (its 7 household cocktails all name a glass).
    /// </para>
    /// <para>
    /// The check sets <c>app.rls_bypass</c> for its transaction: <c>Cocktails</c> forces row-level
    /// security, and a migration has no household, so without it the count would see no rows at all.
    /// </para>
    /// </summary>
    public partial class CocktailGlassRequired : Migration
    {
        /// <summary>Refuses to go on while any cocktail has no glass, naming how many and how to find them.</summary>
        public const string CheckSql =
            """
            SELECT set_config('app.rls_bypass', 'on', true);

            DO $$
            DECLARE missing integer;
            BEGIN
                SELECT count(*) INTO missing FROM "Cocktails" WHERE "GlassTypeId" IS NULL;
                IF missing > 0 THEN
                    RAISE EXCEPTION 'JJ-043: % cocktail(s) have no glass. Give each one its glass first: SELECT "Id", "TenantId", "Name" FROM "Cocktails" WHERE "GlassTypeId" IS NULL;', missing;
                END IF;
            END $$;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CheckSql);

            migrationBuilder.AlterColumn<Guid>(
                name: "GlassTypeId",
                table: "Cocktails",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "GlassTypeId",
                table: "Cocktails",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
