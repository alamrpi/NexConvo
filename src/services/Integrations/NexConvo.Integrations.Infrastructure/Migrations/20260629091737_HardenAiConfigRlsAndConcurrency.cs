using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Integrations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenAiConfigRlsAndConcurrency : Migration
    {
        // NOTE: the model now maps PostgreSQL's system `xmin` column as a concurrency token
        // (WorkspaceAiConfigConfiguration). `xmin` is a system column that already exists on every
        // table, so there is intentionally NO AddColumn here — EF only needs it for the UPDATE
        // WHERE/RETURNING clause at runtime, not as a created column.
        //
        // This migration hardens the RLS policies to match the proven Identity pattern:
        //   * add an explicit WITH CHECK so cross-tenant INSERT/UPDATE is rejected, not just reads;
        //   * use current_setting(..., true) (missing_ok) so a tenant-less connection (migrator,
        //     background work) sees zero rows instead of erroring with "unrecognized parameter".

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS ""TenantIsolation"" ON ""WorkspaceAiConfigs"";
                CREATE POLICY ""TenantIsolation"" ON ""WorkspaceAiConfigs""
                    USING (""TenantId"" = current_setting('app.current_tenant_id', true)::uuid)
                    WITH CHECK (""TenantId"" = current_setting('app.current_tenant_id', true)::uuid);

                DROP POLICY IF EXISTS ""TenantIsolation"" ON ""AuditLogs"";
                CREATE POLICY ""TenantIsolation"" ON ""AuditLogs""
                    USING (""TenantId"" = current_setting('app.current_tenant_id', true)::uuid)
                    WITH CHECK (""TenantId"" = current_setting('app.current_tenant_id', true)::uuid);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS ""TenantIsolation"" ON ""WorkspaceAiConfigs"";
                CREATE POLICY ""TenantIsolation"" ON ""WorkspaceAiConfigs""
                    USING (""TenantId"" = current_setting('app.current_tenant_id')::uuid);

                DROP POLICY IF EXISTS ""TenantIsolation"" ON ""AuditLogs"";
                CREATE POLICY ""TenantIsolation"" ON ""AuditLogs""
                    USING (""TenantId"" = current_setting('app.current_tenant_id')::uuid);
            ");
        }
    }
}
