using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Chat.Infrastructure.Migrations
{
    /// <summary>
    /// Audit remediation for the public widget path:
    ///  - Renames widget columns to snake_case (matching the rest of the table).
    ///  - Repairs empty-string color/welcome values and backfills all-zeros WidgetTokens (legacy rows
    ///    got Guid.Empty), then enforces a UNIQUE index on the token.
    ///  - Creates the <c>resolve_widget_tenant</c> SECURITY DEFINER function — the single RLS-exempt
    ///    lookup the anonymous widget path may perform (Standard 6). Service role gets EXECUTE only.
    /// </summary>
    /// <inheritdoc />
    public partial class WidgetTokenResolutionAndColumnFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WidgetWelcomeMessage",
                table: "workspace_chat_settings",
                newName: "widget_welcome_message");

            migrationBuilder.RenameColumn(
                name: "WidgetToken",
                table: "workspace_chat_settings",
                newName: "widget_token");

            migrationBuilder.RenameColumn(
                name: "WidgetSecondaryColor",
                table: "workspace_chat_settings",
                newName: "widget_secondary_color");

            migrationBuilder.RenameColumn(
                name: "WidgetPrimaryColor",
                table: "workspace_chat_settings",
                newName: "widget_primary_color");

            migrationBuilder.RenameColumn(
                name: "WidgetIconUrl",
                table: "workspace_chat_settings",
                newName: "widget_icon_url");

            migrationBuilder.AlterColumn<string>(
                name: "widget_welcome_message",
                table: "workspace_chat_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "Hi there! How can I help you today?",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "widget_secondary_color",
                table: "workspace_chat_settings",
                type: "character varying(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "#3B82F6",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "widget_primary_color",
                table: "workspace_chat_settings",
                type: "character varying(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "#0F172A",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "widget_icon_url",
                table: "workspace_chat_settings",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            // Repair legacy data BEFORE the unique index: empty-string colors/welcome (created with
            // defaultValue '' by AddWidgetConfig) and all-zeros tokens that would collide on the index.
            migrationBuilder.Sql(@"
                UPDATE workspace_chat_settings SET widget_primary_color   = '#0F172A'                            WHERE widget_primary_color   = '' OR widget_primary_color   IS NULL;
                UPDATE workspace_chat_settings SET widget_secondary_color = '#3B82F6'                            WHERE widget_secondary_color = '' OR widget_secondary_color IS NULL;
                UPDATE workspace_chat_settings SET widget_welcome_message = 'Hi there! How can I help you today?' WHERE widget_welcome_message = '' OR widget_welcome_message IS NULL;

                UPDATE workspace_chat_settings
                SET widget_token = gen_random_uuid()
                WHERE widget_token = '00000000-0000-0000-0000-000000000000'::uuid;

                ALTER TABLE workspace_chat_settings ALTER COLUMN widget_token SET DEFAULT gen_random_uuid();
            ");

            migrationBuilder.CreateIndex(
                name: "idx_workspace_chat_settings_widget_token",
                table: "workspace_chat_settings",
                column: "widget_token",
                unique: true);

            // The single RLS-exempt lookup on the anonymous widget path (Standard 6). Owner-defined,
            // SECURITY DEFINER, so it sees the row regardless of app.current_tenant_id — but returns a
            // tenant ONLY when a Web channel is active. Locked to the service role (EXECUTE only).
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION resolve_widget_tenant(p_widget_token uuid)
                RETURNS uuid
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $$
                    SELECT s.tenant_id
                    FROM workspace_chat_settings s
                    WHERE s.widget_token = p_widget_token
                      AND p_widget_token <> '00000000-0000-0000-0000-000000000000'::uuid
                      AND EXISTS (
                          SELECT 1 FROM channel_connections c
                          WHERE c.tenant_id = s.tenant_id
                            AND c.channel = 0            -- ChatChannel.Web
                            AND c.is_active = true
                      )
                    LIMIT 1;
                $$;

                REVOKE ALL ON FUNCTION resolve_widget_tenant(uuid) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION resolve_widget_tenant(uuid) TO nexconvo_service;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS resolve_widget_tenant(uuid);");

            migrationBuilder.DropIndex(
                name: "idx_workspace_chat_settings_widget_token",
                table: "workspace_chat_settings");

            migrationBuilder.RenameColumn(
                name: "widget_welcome_message",
                table: "workspace_chat_settings",
                newName: "WidgetWelcomeMessage");

            migrationBuilder.RenameColumn(
                name: "widget_token",
                table: "workspace_chat_settings",
                newName: "WidgetToken");

            migrationBuilder.RenameColumn(
                name: "widget_secondary_color",
                table: "workspace_chat_settings",
                newName: "WidgetSecondaryColor");

            migrationBuilder.RenameColumn(
                name: "widget_primary_color",
                table: "workspace_chat_settings",
                newName: "WidgetPrimaryColor");

            migrationBuilder.RenameColumn(
                name: "widget_icon_url",
                table: "workspace_chat_settings",
                newName: "WidgetIconUrl");

            migrationBuilder.AlterColumn<string>(
                name: "WidgetWelcomeMessage",
                table: "workspace_chat_settings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldDefaultValue: "Hi there! How can I help you today?");

            migrationBuilder.AlterColumn<string>(
                name: "WidgetSecondaryColor",
                table: "workspace_chat_settings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(9)",
                oldMaxLength: 9,
                oldDefaultValue: "#3B82F6");

            migrationBuilder.AlterColumn<string>(
                name: "WidgetPrimaryColor",
                table: "workspace_chat_settings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(9)",
                oldMaxLength: 9,
                oldDefaultValue: "#0F172A");

            migrationBuilder.AlterColumn<string>(
                name: "WidgetIconUrl",
                table: "workspace_chat_settings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048,
                oldNullable: true);
        }
    }
}
