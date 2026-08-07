using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Chat.Infrastructure.Migrations
{
    // Every other Chat tenant table pairs ENABLE with FORCE ROW LEVEL SECURITY except these 6
    // (an inconsistency vs. Knowledge/Identity/Integrations, all of which FORCE from their initial
    // schema). FORCE only changes behavior for the table OWNER when that owner is a non-superuser;
    // Chat's runtime connects as nexconvo_service (non-owner, non-superuser), so plain ENABLE is
    // already fully enforced today. This closes the gap for defense-in-depth: if the connection
    // role setup is ever changed to connect as the owning role, or that role loses superuser, RLS
    // keeps filtering instead of silently no-op'ing for the owner.
    /// <inheritdoc />
    public partial class ForceRowLevelSecurityOnChatTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE conversations FORCE ROW LEVEL SECURITY;
                ALTER TABLE messages FORCE ROW LEVEL SECURITY;
                ALTER TABLE escalations FORCE ROW LEVEL SECURITY;
                ALTER TABLE channel_connections FORCE ROW LEVEL SECURITY;
                ALTER TABLE workspace_chat_settings FORCE ROW LEVEL SECURITY;
                ALTER TABLE chat_audit_logs FORCE ROW LEVEL SECURITY;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE conversations NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE messages NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE escalations NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE channel_connections NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE workspace_chat_settings NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE chat_audit_logs NO FORCE ROW LEVEL SECURITY;
            ");
        }
    }
}
