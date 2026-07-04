using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Chat.Infrastructure.Migrations;

public partial class AddDocumentVersionToKnowledgeChunks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
ALTER TABLE knowledge_chunks
    ADD COLUMN IF NOT EXISTS document_version int NOT NULL DEFAULT 1;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
ALTER TABLE knowledge_chunks DROP COLUMN IF EXISTS document_version;
");
    }
}
