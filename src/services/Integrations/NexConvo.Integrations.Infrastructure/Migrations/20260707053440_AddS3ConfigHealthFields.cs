using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Integrations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddS3ConfigHealthFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastTestError",
                table: "WorkspaceS3Configs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastTestLatencyMs",
                table: "WorkspaceS3Configs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastTestStatus",
                table: "WorkspaceS3Configs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastTestedAt",
                table: "WorkspaceS3Configs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastTestError",
                table: "WorkspaceS3Configs");

            migrationBuilder.DropColumn(
                name: "LastTestLatencyMs",
                table: "WorkspaceS3Configs");

            migrationBuilder.DropColumn(
                name: "LastTestStatus",
                table: "WorkspaceS3Configs");

            migrationBuilder.DropColumn(
                name: "LastTestedAt",
                table: "WorkspaceS3Configs");
        }
    }
}
