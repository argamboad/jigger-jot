using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JiggerJot.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// JJ-041 (PREFS-3): converts the recipe lines already in a database to ounces. The seeder and the
    /// authoring handler write ounces from now on through <c>BarMeasure</c> in Core; this is for the rows
    /// that were written before — the shared catalog on an existing database, and any cocktail a
    /// household wrote or forked.
    /// <para>
    /// <b>A frozen copy of <c>BarMeasure</c>'s table and rounding, in SQL,</b> because a migration cannot
    /// call Core. <c>OunceMigrationTests</c> runs this string over rows in the old shape and holds every
    /// line to what <c>BarMeasure.ToStored</c> would have stored, so the copy cannot drift unseen.
    /// </para>
    /// <para>
    /// <b>Sets <c>app.rls_bypass</c> for its own transaction.</b> <c>CocktailIngredients</c> forces row-level
    /// security (JJ-031), and a migration has no household — without the bypass the owner role's UPDATE
    /// would match no row at all and the migration would succeed having changed nothing.
    /// </para>
    /// </summary>
    public partial class OunceCanonicalAmounts : Migration
    {
        /// <summary>The conversion. Safe to run twice: its output is already in ounces on the marks.</summary>
        public const string ConvertSql =
            """
            SELECT set_config('app.rls_bypass', 'on', true);

            -- Parts: each recipe's parts share a three-ounce drink.
            WITH part_totals AS (
                SELECT l."CocktailId", SUM(l."Amount") AS total
                FROM "CocktailIngredients" l
                JOIN "Units" u ON u."Id" = l."UnitId"
                WHERE u."Name" = 'part' AND l."Amount" > 0
                GROUP BY l."CocktailId"
            )
            UPDATE "CocktailIngredients" l
            SET "Amount" = GREATEST(ROUND(l."Amount" / t.total * 3 / 0.25) * 0.25, 0.25),
                "UnitId" = (SELECT "Id" FROM "Units" WHERE "Name" = 'oz')
            FROM part_totals t, "Units" u
            WHERE l."CocktailId" = t."CocktailId"
              AND u."Id" = l."UnitId" AND u."Name" = 'part' AND l."Amount" > 0
              AND EXISTS (SELECT 1 FROM "Units" WHERE "Name" = 'oz');

            -- Every other volume, at the bar's 30 ml ounce, on the quarter marks.
            UPDATE "CocktailIngredients" l
            SET "Amount" = GREATEST(ROUND(l."Amount" * m.millilitres / 30 / 0.25) * 0.25, 0.25),
                "UnitId" = (SELECT "Id" FROM "Units" WHERE "Name" = 'oz')
            FROM "Units" u
            JOIN (VALUES ('oz', 30), ('ml', 1), ('cl', 10), ('l', 1000), ('cup', 240), ('gill', 150),
                         ('pint', 600), ('quart', 1200), ('glass', 60), ('wineglass', 60),
                         ('liqueur glass', 30)) AS m(name, millilitres)
              ON u."Name" = m.name
            WHERE u."Id" = l."UnitId" AND l."Amount" > 0
              AND EXISTS (SELECT 1 FROM "Units" WHERE "Name" = 'oz');
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ConvertSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately nothing. Rounding to the quarter marks and folding parts into three ounces
            // throws information away, so there is no honest way back. The books' own amounts are kept
            // in seed/ and in the embedded cocktails.json, which is where anyone wanting them should look.
        }
    }
}
