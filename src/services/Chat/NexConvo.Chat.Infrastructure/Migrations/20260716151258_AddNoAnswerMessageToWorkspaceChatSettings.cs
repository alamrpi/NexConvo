using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Chat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNoAnswerMessageToWorkspaceChatSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "no_answer_message",
                table: "workspace_chat_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "Sorry, I don't have information about that. Please contact our support team for help.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "no_answer_message",
                table: "workspace_chat_settings");
        }
    }
}
