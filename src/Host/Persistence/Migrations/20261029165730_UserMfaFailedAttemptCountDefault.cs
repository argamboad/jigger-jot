using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JiggerJot.Host.Persistence.Migrations
{
    /// <summary>
    /// The pilot's catch-up (Pkg J.3): the platform's 1.0.0-alpha.3 schema change, as the platform wrote it
    /// (perezosoft-platform Pkg 5.8, #451). The model says what MfaLockout's AddColumn(defaultValue: 0) already put in the
    /// database — a default of 0 on UserMfa.FailedAttemptCount — so Up re-states it (no change where MfaLockout ran) and
    /// Down leaves it, because it predates this migration and stays MfaLockout's to remove.
    /// </summary>
    public partial class UserMfaFailedAttemptCountDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "FailedAttemptCount",
                table: "UserMfa",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing: the default predates this migration (MfaLockout's AddColumn); dropping it here would leave a database
            // that ran MfaLockout without the default MfaLockout gave it.
        }
    }
}
