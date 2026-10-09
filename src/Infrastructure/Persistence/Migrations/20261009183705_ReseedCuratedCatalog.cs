using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JiggerJot.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// JJ-043 (#181): clears the SHARED recipe catalog so the seeder writes the curated one in its place.
    /// <para>
    /// <c>CatalogSeeder</c> only ever adds what is missing, so a database seeded before JJ-043 keeps every
    /// shared recipe it had — staging carried all 969 extracted recipes, a local one the 31-recipe starter
    /// set — beside the curated 644, and keeps the old names on the recipes that stayed. Deleting the shared
    /// rows here lets the next startup seed the curated set whole. The ids derive from source + slug, so a
    /// recipe that stays comes back with the id it had.
    /// </para>
    /// <para>
    /// <b>Only shared rows</b> (<c>TenantId IS NULL</c>); a household's own recipes and forks are untouched.
    /// A fork survives its original's deletion because <c>ForkedFromCocktailId</c> is provenance and not a
    /// foreign key (JJ-013); a fork of a recipe that stays links to it again once it is reseeded. The lines
    /// go by the foreign key's cascade. Ingredients, lookups and substitutions are left as they are.
    /// </para>
    /// <para>
    /// <b>Sets <c>app.rls_bypass</c> for its own transaction</b>, like <c>OunceCanonicalAmounts</c>: both
    /// tables force row-level security, and the write policies never see a shared row (JJ-031), so without
    /// it the delete would match nothing and the migration would succeed having changed nothing.
    /// </para>
    /// </summary>
    public partial class ReseedCuratedCatalog : Migration
    {
        /// <summary>The delete. Safe on an empty database and safe to run twice.</summary>
        public const string ClearSharedCatalogSql =
            """
            SELECT set_config('app.rls_bypass', 'on', true);

            DELETE FROM "Cocktails" WHERE "TenantId" IS NULL;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ClearSharedCatalogSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately nothing. The shared catalog is the seed file's to write, and the seeder writes
            // whatever the deployed file holds on the next startup.
        }
    }
}
