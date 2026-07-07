using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Chat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelConnectionHealthFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "last_test_error",
                table: "channel_connections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "last_test_latency_ms",
                table: "channel_connections",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "last_test_status",
                table: "channel_connections",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_tested_at",
                table: "channel_connections",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_test_error",
                table: "channel_connections");

            migrationBuilder.DropColumn(
                name: "last_test_latency_ms",
                table: "channel_connections");

            migrationBuilder.DropColumn(
                name: "last_test_status",
                table: "channel_connections");

            migrationBuilder.DropColumn(
                name: "last_tested_at",
                table: "channel_connections");
        }
    }
}
