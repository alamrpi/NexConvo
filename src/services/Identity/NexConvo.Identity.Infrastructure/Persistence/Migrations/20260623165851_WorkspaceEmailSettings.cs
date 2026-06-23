using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceEmailSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workspace_email_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    from_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    from_address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    smtp_host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    smtp_port = table.Column<int>(type: "integer", nullable: false),
                    smtp_username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    smtp_use_ssl = table.Column<bool>(type: "boolean", nullable: false),
                    encrypted_secret = table.Column<string>(type: "text", nullable: true),
                    last_tested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_test_succeeded = table.Column<bool>(type: "boolean", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_email_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_email_settings_tenant_id",
                table: "workspace_email_settings",
                column: "tenant_id",
                unique: true);

            // Tenant-scoped (skill Standard 6) — same RLS pattern as the initial migration.
            migrationBuilder.Sql(
                "GRANT SELECT, INSERT, UPDATE, DELETE ON workspace_email_settings TO nexconvo_service;");
            migrationBuilder.Sql("ALTER TABLE workspace_email_settings ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE workspace_email_settings FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                "CREATE POLICY tenant_isolation ON workspace_email_settings " +
                "USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid) " +
                "WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workspace_email_settings");
        }
    }
}
