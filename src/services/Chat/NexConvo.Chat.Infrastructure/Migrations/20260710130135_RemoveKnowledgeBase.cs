using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Chat.Infrastructure.Migrations
{
    /// <summary>
    /// The knowledge base moved to the Knowledge service (database-per-service, Standard 5) —
    /// Chat's copies of the tables are dropped. IF EXISTS because fresh databases never ran the
    /// deleted knowledge creation migrations, so the tables may legitimately be absent.
    /// The vector extension stays installed (harmless, and dropping it could affect other objects).
    /// </summary>
    public partial class RemoveKnowledgeBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS knowledge_chunks;
DROP TABLE IF EXISTS knowledge_documents;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible by design: the knowledge schema now lives in the Knowledge service.
        }
    }
}
