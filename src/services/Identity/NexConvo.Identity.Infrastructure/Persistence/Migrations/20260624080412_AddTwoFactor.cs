using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "encrypted_totp_secret",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "two_factor_backup_codes",
                table: "users",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb"); // empty array, not "" (invalid JSON)

            migrationBuilder.AddColumn<bool>(
                name: "two_factor_enabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "encrypted_totp_secret",
                table: "users");

            migrationBuilder.DropColumn(
                name: "two_factor_backup_codes",
                table: "users");

            migrationBuilder.DropColumn(
                name: "two_factor_enabled",
                table: "users");
        }
    }
}
