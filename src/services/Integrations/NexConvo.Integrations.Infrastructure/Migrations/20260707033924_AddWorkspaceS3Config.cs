using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Integrations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceS3Config : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkspaceS3Configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BucketName = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    Region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EncryptedAccessKeyId = table.Column<string>(type: "text", nullable: false),
                    EncryptedSecretAccessKey = table.Column<string>(type: "text", nullable: false),
                    CustomEndpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PathPrefix = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceS3Configs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceS3Configs_TenantId",
                table: "WorkspaceS3Configs",
                column: "TenantId",
                unique: true);

            // Enable RLS + hardened policies (WITH CHECK + missing_ok) matching the established pattern.
            migrationBuilder.Sql(@"
                ALTER TABLE ""WorkspaceS3Configs"" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ""WorkspaceS3Configs"" FORCE ROW LEVEL SECURITY;

                CREATE POLICY ""TenantIsolation"" ON ""WorkspaceS3Configs""
                    USING (""TenantId"" = current_setting('app.current_tenant_id', true)::uuid)
                    WITH CHECK (""TenantId"" = current_setting('app.current_tenant_id', true)::uuid);

                GRANT SELECT, INSERT, UPDATE, DELETE ON ""WorkspaceS3Configs"" TO nexconvo_service;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"REVOKE ALL ON ""WorkspaceS3Configs"" FROM nexconvo_service;");

            migrationBuilder.DropTable(
                name: "WorkspaceS3Configs");
        }
    }
}
