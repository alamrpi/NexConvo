using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Chat.Infrastructure.Migrations
{
    // AddOutboxInboxTables (the prior migration) created InboxState/OutboxMessage/OutboxState via
    // EF's generated CreateTable calls, which do not GRANT anything to nexconvo_service — every
    // other Chat migration's raw SQL does this explicitly. Without it, the app (which connects as
    // nexconvo_service, not the migration-running superuser) gets "permission denied" the moment
    // MassTransit's bus outbox delivery service tries to read OutboxState, silently breaking the
    // outbox/inbox in any real deployment. Caught by NexConvo.Chat.IntegrationTests exercising the
    // real nexconvo_service role end-to-end (Standard 6's RLS convention already uses this role;
    // these tables have no RLS, per AddOutboxInboxTables' own comment, but still need base grants).
    /// <inheritdoc />
    public partial class GrantOutboxInboxTablePrivileges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE, DELETE ON ""InboxState"" TO nexconvo_service;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ""OutboxMessage"" TO nexconvo_service;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ""OutboxState"" TO nexconvo_service;
                GRANT USAGE, SELECT ON ""OutboxMessage_SequenceNumber_seq"" TO nexconvo_service;
                GRANT USAGE, SELECT ON ""InboxState_Id_seq"" TO nexconvo_service;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                REVOKE SELECT, INSERT, UPDATE, DELETE ON ""InboxState"" FROM nexconvo_service;
                REVOKE SELECT, INSERT, UPDATE, DELETE ON ""OutboxMessage"" FROM nexconvo_service;
                REVOKE SELECT, INSERT, UPDATE, DELETE ON ""OutboxState"" FROM nexconvo_service;
                REVOKE USAGE, SELECT ON ""OutboxMessage_SequenceNumber_seq"" FROM nexconvo_service;
                REVOKE USAGE, SELECT ON ""InboxState_Id_seq"" FROM nexconvo_service;
            ");
        }
    }
}
