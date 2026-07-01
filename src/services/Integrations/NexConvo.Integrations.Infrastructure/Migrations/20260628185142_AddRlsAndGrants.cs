using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Integrations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRlsAndGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""WorkspaceAiConfigs"" ENABLE ROW LEVEL SECURITY;
                CREATE POLICY ""TenantIsolation"" ON ""WorkspaceAiConfigs""
                    USING (""TenantId"" = current_setting('app.current_tenant_id')::uuid);
                GRANT SELECT, INSERT, UPDATE, DELETE ON ""WorkspaceAiConfigs"" TO nexconvo_service;

                ALTER TABLE ""AuditLogs"" ENABLE ROW LEVEL SECURITY;
                CREATE POLICY ""TenantIsolation"" ON ""AuditLogs""
                    USING (""TenantId"" = current_setting('app.current_tenant_id')::uuid);
                GRANT SELECT, INSERT, UPDATE, DELETE ON ""AuditLogs"" TO nexconvo_service;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
