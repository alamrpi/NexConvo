using FluentAssertions;
using Npgsql;

namespace NexConvo.Knowledge.IntegrationTests;

/// <summary>
/// Proves the fresh migrations produced the schema CHATBOT-ARCHITECTURE.md §12/§13 requires:
/// vector(1024), the HNSW cosine index, forced RLS on every table, and the dedup unique index.
/// </summary>
[Collection("knowledge-postgres")]
public class KnowledgeSchemaTests(KnowledgePostgresFixture fixture)
{
    private async Task<T?> ScalarAsync<T>(string sql)
    {
        await using var conn = new NpgsqlConnection(fixture.SuperuserConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        return (T?)await cmd.ExecuteScalarAsync();
    }

    [Fact]
    public async Task EmbeddingColumn_Is_Vector1024()
    {
        var type = await ScalarAsync<string>(@"
            SELECT format_type(atttypid, atttypmod)
            FROM pg_attribute
            WHERE attrelid = 'knowledge_chunks'::regclass AND attname = 'embedding'");

        type.Should().Be("vector(1024)");
    }

    [Fact]
    public async Task HnswCosineIndex_Exists_OnEmbedding()
    {
        var indexDef = await ScalarAsync<string>(@"
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'knowledge_chunks'
              AND indexname = 'idx_knowledge_chunks_embedding_hnsw'");

        indexDef.Should().NotBeNull();
        indexDef.Should().Contain("USING hnsw").And.Contain("vector_cosine_ops");
    }

    [Theory]
    [InlineData("knowledge_documents")]
    [InlineData("knowledge_chunks")]
    [InlineData("knowledge_audit_logs")]
    [Trait("Category", "Security")]
    public async Task RowLevelSecurity_IsEnabledAndForced(string table)
    {
        var enabled = await ScalarAsync<bool>(
            $"SELECT relrowsecurity FROM pg_class WHERE relname = '{table}'");
        var forced = await ScalarAsync<bool>(
            $"SELECT relforcerowsecurity FROM pg_class WHERE relname = '{table}'");

        enabled.Should().BeTrue($"{table} must have RLS enabled");
        forced.Should().BeTrue($"{table} must FORCE RLS (owner is not exempt)");
    }

    [Fact]
    public async Task ContentHashDedupIndex_IsUnique_PerTenant()
    {
        var indexDef = await ScalarAsync<string>(@"
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'knowledge_documents'
              AND indexname = 'idx_knowledge_documents_content_hash'");

        indexDef.Should().NotBeNull();
        indexDef.Should().StartWith("CREATE UNIQUE INDEX");
        indexDef.Should().Contain("tenant_id").And.Contain("content_hash");
    }
}
