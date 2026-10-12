using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JiggerJot.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The reader's measuring system moves out of the platform's Users table into its own user-keyed table (Arch A3,
    /// jigger-jot#164): the table is created, every stored choice copied across, and the old "PreferredUnitSystem"
    /// column dropped, so Users is back to the platform's shape (PlatformSchemaTests, Arch A5). Down restores the column
    /// and copies every choice back, so a rollback loses nothing but the rows' timestamps.
    /// </summary>
    public partial class AddUserUnitPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserUnitPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitSystem = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserUnitPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserUnitPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserUnitPreferences_UserId",
                table: "UserUnitPreferences",
                column: "UserId",
                unique: true);

            // Copy every stored choice (Metric = 0, Imperial = 1; Neutral was never a reader's choice), then drop the column.
            migrationBuilder.Sql("""
                INSERT INTO "UserUnitPreferences" ("Id", "UserId", "UnitSystem", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), u."Id", u."PreferredUnitSystem", now(), now()
                FROM "Users" u
                WHERE u."PreferredUnitSystem" IN (0, 1);
                """);

            migrationBuilder.DropColumn(
                name: "PreferredUnitSystem",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data loss on Down: only the rows' timestamps. The column comes back and every choice, including one made
            // after Up, is copied into it before the table goes.
            migrationBuilder.AddColumn<int>(
                name: "PreferredUnitSystem",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Users" u SET "PreferredUnitSystem" = p."UnitSystem"
                FROM "UserUnitPreferences" p WHERE p."UserId" = u."Id";
                """);

            migrationBuilder.DropTable(
                name: "UserUnitPreferences");
        }
    }
}
