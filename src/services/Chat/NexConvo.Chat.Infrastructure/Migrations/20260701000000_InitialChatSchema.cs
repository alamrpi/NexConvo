using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Chat.Infrastructure.Migrations
{
    public partial class InitialChatSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── channel_connections ────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS channel_connections (
                    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                    tenant_id uuid NOT NULL,
                    channel smallint NOT NULL,
                    external_account_id text,
                    display_name text NOT NULL,
                    encrypted_access_token text NOT NULL,
                    encrypted_app_secret text,
                    verify_token text NOT NULL,
                    is_active boolean NOT NULL DEFAULT true,
                    created_at timestamptz NOT NULL DEFAULT now(),
                    updated_at timestamptz NOT NULL DEFAULT now(),
                    created_by_user_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'
                );

                CREATE UNIQUE INDEX IF NOT EXISTS idx_channel_connections_unique_active
                    ON channel_connections (tenant_id, channel, external_account_id)
                    WHERE is_active = true;

                CREATE INDEX IF NOT EXISTS idx_channel_connections_tenant_id
                    ON channel_connections (tenant_id);

                ALTER TABLE channel_connections ENABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS ""TenantIsolation"" ON channel_connections;
                CREATE POLICY ""TenantIsolation"" ON channel_connections
                    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
                    WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

                GRANT SELECT, INSERT, UPDATE, DELETE ON channel_connections TO nexconvo_service;
            ");

            // ── workspace_chat_settings ────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS workspace_chat_settings (
                    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                    tenant_id uuid UNIQUE NOT NULL,
                    primary_provider smallint NOT NULL DEFAULT 0,
                    primary_model text NOT NULL DEFAULT 'gpt-4o-mini',
                    fallback_providers jsonb NOT NULL DEFAULT '[]',
                    system_prompt_override text,
                    handoff_confidence_threshold float8 NOT NULL DEFAULT 0.65,
                    sentiment_escalation_enabled boolean NOT NULL DEFAULT true,
                    sentiment_sensitivity smallint NOT NULL DEFAULT 1,
                    trigger_phrases jsonb NOT NULL DEFAULT '[]',
                    max_unanswered_messages int NOT NULL DEFAULT 3,
                    pii_masking_level smallint NOT NULL DEFAULT 1,
                    data_retention_days int,
                    created_at timestamptz NOT NULL DEFAULT now(),
                    updated_at timestamptz NOT NULL DEFAULT now(),
                    created_by_user_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'
                );

                CREATE INDEX IF NOT EXISTS idx_workspace_chat_settings_tenant_id
                    ON workspace_chat_settings (tenant_id);

                ALTER TABLE workspace_chat_settings ENABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS ""TenantIsolation"" ON workspace_chat_settings;
                CREATE POLICY ""TenantIsolation"" ON workspace_chat_settings
                    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
                    WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

                GRANT SELECT, INSERT, UPDATE, DELETE ON workspace_chat_settings TO nexconvo_service;
            ");

            // ── chat_audit_logs ────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS chat_audit_logs (
                    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                    tenant_id uuid NOT NULL,
                    user_id uuid,
                    action text NOT NULL,
                    detail text,
                    occurred_at timestamptz NOT NULL DEFAULT now()
                );

                CREATE INDEX IF NOT EXISTS idx_chat_audit_logs_tenant_id
                    ON chat_audit_logs (tenant_id);

                CREATE INDEX IF NOT EXISTS idx_chat_audit_logs_occurred_at
                    ON chat_audit_logs (occurred_at);

                ALTER TABLE chat_audit_logs ENABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS ""TenantIsolation"" ON chat_audit_logs;
                CREATE POLICY ""TenantIsolation"" ON chat_audit_logs
                    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
                    WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

                GRANT SELECT, INSERT, UPDATE, DELETE ON chat_audit_logs TO nexconvo_service;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS chat_audit_logs;
                DROP TABLE IF EXISTS workspace_chat_settings;
                DROP TABLE IF EXISTS channel_connections;
            ");
        }
    }
}
