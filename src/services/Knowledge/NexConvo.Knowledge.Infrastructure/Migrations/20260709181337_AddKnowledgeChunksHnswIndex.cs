using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Knowledge.Infrastructure.Migrations
{
    /// <summary>
    /// HNSW cosine index over the 1024-dim embedding column (CHATBOT-ARCHITECTURE.md §12).
    /// Separate migration because HNSW index creation can't share the table-creation
    /// DDL transaction.
    /// </summary>
    public partial class AddKnowledgeChunksHnswIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE INDEX idx_knowledge_chunks_embedding_hnsw
    ON knowledge_chunks
    USING hnsw (embedding vector_cosine_ops)
    WITH (m = 16, ef_construction = 64);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS idx_knowledge_chunks_embedding_hnsw;
");
        }
    }
}
