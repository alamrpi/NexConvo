using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Integrations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiConfigHealthFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastTestError",
                table: "WorkspaceAiConfigs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastTestLatencyMs",
                table: "WorkspaceAiConfigs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastTestStatus",
                table: "WorkspaceAiConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastTestedAt",
                table: "WorkspaceAiConfigs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastTestError",
                table: "WorkspaceAiConfigs");

            migrationBuilder.DropColumn(
                name: "LastTestLatencyMs",
                table: "WorkspaceAiConfigs");

            migrationBuilder.DropColumn(
                name: "LastTestStatus",
                table: "WorkspaceAiConfigs");

            migrationBuilder.DropColumn(
                name: "LastTestedAt",
                table: "WorkspaceAiConfigs");
        }
    }
}
