using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Chat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── conversations ──────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS conversations (
                    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                    tenant_id uuid NOT NULL,
                    channel smallint NOT NULL,
                    external_conversation_id text NOT NULL,
                    state smallint NOT NULL DEFAULT 0,
                    contact_id uuid,
                    assigned_agent_user_id uuid,
                    last_inbound_provider_message_id text,
                    created_at timestamptz NOT NULL DEFAULT now(),
                    updated_at timestamptz NOT NULL DEFAULT now(),
                    created_by_user_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'
                );

                CREATE UNIQUE INDEX IF NOT EXISTS idx_conversations_unique_open_thread
                    ON conversations (tenant_id, channel, external_conversation_id)
                    WHERE state IN (0, 1, 2); -- AiHandling, PendingHuman, HumanHandling

                CREATE INDEX IF NOT EXISTS ix_conversations_tenant_id ON conversations (tenant_id);

                ALTER TABLE conversations ENABLE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS ""TenantIsolation"" ON conversations;
                CREATE POLICY ""TenantIsolation"" ON conversations
                    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
                    WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
                GRANT SELECT, INSERT, UPDATE, DELETE ON conversations TO nexconvo_service;
            ");

            // ── messages ───────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS messages (
                    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                    tenant_id uuid NOT NULL,
                    conversation_id uuid NOT NULL REFERENCES conversations(id),
                    sender_role smallint NOT NULL,
                    sender_display_ref text,
                    sender_user_id uuid,
                    direction smallint NOT NULL,
                    body text NOT NULL,
                    provider_message_id text,
                    delivery_status smallint NOT NULL DEFAULT 0,
                    confidence float8,
                    structured_payload jsonb,
                    created_at timestamptz NOT NULL DEFAULT now()
                );

                CREATE INDEX IF NOT EXISTS ix_messages_tenant_conversation ON messages (tenant_id, conversation_id);
                CREATE INDEX IF NOT EXISTS ix_messages_structured_payload_gin ON messages USING gin (structured_payload);

                ALTER TABLE messages ENABLE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS ""TenantIsolation"" ON messages;
                CREATE POLICY ""TenantIsolation"" ON messages
                    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
                    WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
                GRANT SELECT, INSERT, UPDATE, DELETE ON messages TO nexconvo_service;
            ");

            // ── escalations ────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS escalations (
                    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                    tenant_id uuid NOT NULL,
                    conversation_id uuid NOT NULL REFERENCES conversations(id),
                    reason smallint NOT NULL,
                    raised_at timestamptz NOT NULL DEFAULT now(),
                    accepted_by_user_id uuid,
                    accepted_at timestamptz,
                    resolved_at timestamptz
                );

                CREATE INDEX IF NOT EXISTS ix_escalations_tenant_conversation ON escalations (tenant_id, conversation_id);

                ALTER TABLE escalations ENABLE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS ""TenantIsolation"" ON escalations;
                CREATE POLICY ""TenantIsolation"" ON escalations
                    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
                    WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
                GRANT SELECT, INSERT, UPDATE, DELETE ON escalations TO nexconvo_service;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS messages;
                DROP TABLE IF EXISTS escalations;
                DROP TABLE IF EXISTS conversations CASCADE;
            ");
        }
    }
}
