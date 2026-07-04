using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Chat.Infrastructure.Migrations;

/// <summary>
/// Adds the HNSW index on knowledge_chunks.embedding in a separate migration.
/// HNSW index creation requires the pgvector extension (already installed in the prior migration)
/// and cannot be in the same transaction as table creation on some PostgreSQL setups.
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
        migrationBuilder.Sql("DROP INDEX IF EXISTS idx_knowledge_chunks_embedding_hnsw;");
    }
}
