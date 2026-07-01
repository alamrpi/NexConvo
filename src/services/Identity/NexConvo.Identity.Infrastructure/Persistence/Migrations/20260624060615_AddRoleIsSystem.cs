using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleIsSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_system",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Existing tenants already have Owner/Admin seeded — mark them immutable.
            migrationBuilder.Sql("UPDATE roles SET is_system = TRUE WHERE name IN ('Owner', 'Admin');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_system",
                table: "roles");
        }
    }
}
