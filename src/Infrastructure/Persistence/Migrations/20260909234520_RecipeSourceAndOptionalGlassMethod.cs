using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JiggerJot.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// What seeding real recipes turned out to require (SEED-3).
    /// <para>
    /// <b>RecipeSources</b> so a credit is a property of the row (JJ-032). The catalog mixes sources,
    /// so a flat attribution page cannot say which drink came from where, and pulling a source later
    /// would mean re-deriving which rows to remove. Another curated global lookup: no tenant column,
    /// and therefore no RLS policy, exactly like GlassTypes.
    /// </para>
    /// <para>
    /// <b>Cocktails.GlassTypeId and MethodId become nullable</b> (JJ-034). 259 of the 969 seeded
    /// recipes either state no glass or state something that is not a glass type - the Savoy's
    /// "medium size glass" and bare "glass" account for 163 on their own. Filling those in would put
    /// a fact in the database that nobody wrote down, and it would be indistinguishable afterwards
    /// from a fact that was. Null reads as "not specified" and filters as such.
    /// </para>
    /// </summary>
    public partial class RecipeSourceAndOptionalGlassMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "MethodId",
                table: "Cocktails",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "GlassTypeId",
                table: "Cocktails",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceId",
                table: "Cocktails",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecipeSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Attribution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeSources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cocktails_SourceId",
                table: "Cocktails",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSources_Name",
                table: "RecipeSources",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cocktails_RecipeSources_SourceId",
                table: "Cocktails",
                column: "SourceId",
                principalTable: "RecipeSources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cocktails_RecipeSources_SourceId",
                table: "Cocktails");

            migrationBuilder.DropTable(
                name: "RecipeSources");

            migrationBuilder.DropIndex(
                name: "IX_Cocktails_SourceId",
                table: "Cocktails");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "Cocktails");

            migrationBuilder.AlterColumn<Guid>(
                name: "MethodId",
                table: "Cocktails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "GlassTypeId",
                table: "Cocktails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
