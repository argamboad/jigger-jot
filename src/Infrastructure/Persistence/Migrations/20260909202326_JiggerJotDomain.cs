using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JiggerJot.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The whole JiggerJot domain in one migration (JJ-031, epic CKTL): the four curated global lookups
    /// (IngredientCategories, GlassTypes, Methods, Units), the global substitution graph, the three
    /// dual-natured catalog tables (Ingredients, Cocktails, CocktailIngredients) and the household shelf
    /// (TenantInventories) - plus the row-level-security policies that make the tenancy backstop
    /// (ADR-020) cover all of them from the moment the tables exist.
    /// <para>
    /// TenantInventories is ordinary ITenantScoped data and takes the platform's frozen RlsDdl output.
    /// The three dual-natured tables cannot: their TenantId is nullable and a NULL means "shared catalog,
    /// readable by every household", which the platform's <c>TenantId = current</c> predicate matches
    /// never. They take the asymmetric shared-or-tenant policy set instead - SELECT admits shared rows,
    /// INSERT/UPDATE/DELETE do not - so the read-only-catalog rule (JJ-002) holds at the database and not
    /// merely in the query filter. Seeding the shared catalog is a bypass-GUC operation by construction.
    /// </para>
    /// <para>
    /// The SQL below is the frozen output of RlsDdl.StatementsFor / RlsDdl.SharedOrTenantStatementsFor for
    /// these tables; app tests assert the migrated schema still matches the model. Down() drops the tables,
    /// which takes their policies with them - no explicit policy teardown needed.
    /// </para>
    /// </summary>
    public partial class JiggerJotDomain : Migration
    {
        // Frozen table lists for the RLS DDL below - the migration must never re-derive them from the
        // live model, or a later entity would retroactively change what this migration already did.
        private static readonly string[] TenantScopedTables = ["TenantInventories"];
        private static readonly string[] SharedOrTenantTables = ["CocktailIngredients", "Cocktails", "Ingredients"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreferredUnitSystem",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GlassTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlassTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IngredientCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientCategories_IngredientCategories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "IngredientCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Methods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Methods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    System = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MillilitreFactor = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubcategoryId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ingredients_IngredientCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "IngredientCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ingredients_IngredientCategories_SubcategoryId",
                        column: x => x.SubcategoryId,
                        principalTable: "IngredientCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cocktails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ForkedFromCocktailId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GlassTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MethodId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cocktails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cocktails_GlassTypes_GlassTypeId",
                        column: x => x.GlassTypeId,
                        principalTable: "GlassTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cocktails_Methods_MethodId",
                        column: x => x.MethodId,
                        principalTable: "Methods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientSubstitutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubstituteIngredientId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientSubstitutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientSubstitutions_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientSubstitutions_Ingredients_SubstituteIngredientId",
                        column: x => x.SubstituteIngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantInventories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantInventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantInventories_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CocktailIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CocktailId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CocktailIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CocktailIngredients_Cocktails_CocktailId",
                        column: x => x.CocktailId,
                        principalTable: "Cocktails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CocktailIngredients_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CocktailIngredients_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CocktailIngredients_CocktailId",
                table: "CocktailIngredients",
                column: "CocktailId");

            migrationBuilder.CreateIndex(
                name: "IX_CocktailIngredients_CocktailId_IsRequired",
                table: "CocktailIngredients",
                columns: new[] { "CocktailId", "IsRequired" });

            migrationBuilder.CreateIndex(
                name: "IX_CocktailIngredients_IngredientId",
                table: "CocktailIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_CocktailIngredients_TenantId",
                table: "CocktailIngredients",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CocktailIngredients_UnitId",
                table: "CocktailIngredients",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Cocktails_ForkedFromCocktailId",
                table: "Cocktails",
                column: "ForkedFromCocktailId");

            migrationBuilder.CreateIndex(
                name: "IX_Cocktails_GlassTypeId",
                table: "Cocktails",
                column: "GlassTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Cocktails_MethodId",
                table: "Cocktails",
                column: "MethodId");

            migrationBuilder.CreateIndex(
                name: "IX_Cocktails_TenantId",
                table: "Cocktails",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Cocktails_TenantId_Name",
                table: "Cocktails",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_GlassTypes_Name",
                table: "GlassTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCategories_ParentId",
                table: "IngredientCategories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCategories_ParentId_Name",
                table: "IngredientCategories",
                columns: new[] { "ParentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_CategoryId",
                table: "Ingredients",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_SubcategoryId",
                table: "Ingredients",
                column: "SubcategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_TenantId",
                table: "Ingredients",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_TenantId_CategoryId",
                table: "Ingredients",
                columns: new[] { "TenantId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_TenantId_Name_Unique",
                table: "Ingredients",
                columns: new[] { "TenantId", "Name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSubstitutions_IngredientId",
                table: "IngredientSubstitutions",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSubstitutions_IngredientId_SubstituteIngredientId",
                table: "IngredientSubstitutions",
                columns: new[] { "IngredientId", "SubstituteIngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSubstitutions_SubstituteIngredientId",
                table: "IngredientSubstitutions",
                column: "SubstituteIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_Methods_Name",
                table: "Methods",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantInventories_IngredientId",
                table: "TenantInventories",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantInventories_TenantId",
                table: "TenantInventories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantInventories_TenantId_IngredientId",
                table: "TenantInventories",
                columns: new[] { "TenantId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Units_Name",
                table: "Units",
                column: "Name",
                unique: true);

            // -- ADR-020 tenancy backstop for the tables created above ---------------------------------
            // Frozen output of RlsDdl.StatementsFor for the new ITenantScoped table.
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"""ALTER TABLE "{table}" ENABLE ROW LEVEL SECURITY;""");
                migrationBuilder.Sql($"""ALTER TABLE "{table}" FORCE ROW LEVEL SECURITY;""");
                migrationBuilder.Sql($"""DROP POLICY IF EXISTS rls_tenant_isolation ON "{table}";""");
                migrationBuilder.Sql(
                    $"""
                     CREATE POLICY rls_tenant_isolation ON "{table}"
                         AS PERMISSIVE FOR ALL
                         USING ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                                OR current_setting('app.rls_bypass', true) = 'on')
                         WITH CHECK ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                                     OR current_setting('app.rls_bypass', true) = 'on');
                     """);
            }

            // Frozen output of RlsDdl.SharedOrTenantStatementsFor for the dual-natured tables (JJ-031).
            // Deliberately asymmetric: SELECT sees the shared catalog, the three write commands never do -
            // one FOR ALL policy would have let a household DELETE catalog rows, since DELETE is checked
            // against USING and WITH CHECK never applies to it.
            foreach (var table in SharedOrTenantTables)
            {
                migrationBuilder.Sql($"""ALTER TABLE "{table}" ENABLE ROW LEVEL SECURITY;""");
                migrationBuilder.Sql($"""ALTER TABLE "{table}" FORCE ROW LEVEL SECURITY;""");

                migrationBuilder.Sql($"""DROP POLICY IF EXISTS rls_shared_or_tenant_select ON "{table}";""");
                migrationBuilder.Sql(
                    $"""
                     CREATE POLICY rls_shared_or_tenant_select ON "{table}"
                         AS PERMISSIVE FOR SELECT
                         USING ("TenantId" IS NULL
                                OR "TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                                OR current_setting('app.rls_bypass', true) = 'on');
                     """);

                migrationBuilder.Sql($"""DROP POLICY IF EXISTS rls_shared_or_tenant_insert ON "{table}";""");
                migrationBuilder.Sql(
                    $"""
                     CREATE POLICY rls_shared_or_tenant_insert ON "{table}"
                         AS PERMISSIVE FOR INSERT
                         WITH CHECK ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                                     OR current_setting('app.rls_bypass', true) = 'on');
                     """);

                migrationBuilder.Sql($"""DROP POLICY IF EXISTS rls_shared_or_tenant_update ON "{table}";""");
                migrationBuilder.Sql(
                    $"""
                     CREATE POLICY rls_shared_or_tenant_update ON "{table}"
                         AS PERMISSIVE FOR UPDATE
                         USING ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                                OR current_setting('app.rls_bypass', true) = 'on')
                         WITH CHECK ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                                     OR current_setting('app.rls_bypass', true) = 'on');
                     """);

                migrationBuilder.Sql($"""DROP POLICY IF EXISTS rls_shared_or_tenant_delete ON "{table}";""");
                migrationBuilder.Sql(
                    $"""
                     CREATE POLICY rls_shared_or_tenant_delete ON "{table}"
                         AS PERMISSIVE FOR DELETE
                         USING ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                                OR current_setting('app.rls_bypass', true) = 'on');
                     """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CocktailIngredients");

            migrationBuilder.DropTable(
                name: "IngredientSubstitutions");

            migrationBuilder.DropTable(
                name: "TenantInventories");

            migrationBuilder.DropTable(
                name: "Cocktails");

            migrationBuilder.DropTable(
                name: "Units");

            migrationBuilder.DropTable(
                name: "Ingredients");

            migrationBuilder.DropTable(
                name: "GlassTypes");

            migrationBuilder.DropTable(
                name: "Methods");

            migrationBuilder.DropTable(
                name: "IngredientCategories");

            migrationBuilder.DropColumn(
                name: "PreferredUnitSystem",
                table: "Users");
        }
    }
}
