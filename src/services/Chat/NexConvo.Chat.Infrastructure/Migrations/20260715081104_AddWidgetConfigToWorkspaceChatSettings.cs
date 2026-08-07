using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Chat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWidgetConfigToWorkspaceChatSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WidgetIconUrl",
                table: "workspace_chat_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WidgetPrimaryColor",
                table: "workspace_chat_settings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WidgetSecondaryColor",
                table: "workspace_chat_settings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "WidgetToken",
                table: "workspace_chat_settings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "WidgetWelcomeMessage",
                table: "workspace_chat_settings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WidgetIconUrl",
                table: "workspace_chat_settings");

            migrationBuilder.DropColumn(
                name: "WidgetPrimaryColor",
                table: "workspace_chat_settings");

            migrationBuilder.DropColumn(
                name: "WidgetSecondaryColor",
                table: "workspace_chat_settings");

            migrationBuilder.DropColumn(
                name: "WidgetToken",
                table: "workspace_chat_settings");

            migrationBuilder.DropColumn(
                name: "WidgetWelcomeMessage",
                table: "workspace_chat_settings");
        }
    }
}
