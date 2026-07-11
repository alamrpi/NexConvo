using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Domain.Entities;
using NexConvo.Knowledge.Infrastructure.Multitenancy;
using NexConvo.Knowledge.Infrastructure.Persistence;
using Npgsql;

namespace NexConvo.Knowledge.IntegrationTests;

[Collection("knowledge-postgres")]
public class KnowledgeChunkWriterTests(KnowledgePostgresFixture fixture)
{
    private static float[] RandomEmbedding(int dimensions)
    {
        var random = new Random(42);
        var values = new float[dimensions];
        for (var i = 0; i < dimensions; i++)
            values[i] = (float)random.NextDouble();
        return values;
    }

    private async Task<KnowledgeDocument> SeedDocumentAsync(Guid tenantId, string contentHash)
    {
        await using var db = fixture.CreateTenantContext(tenantId);
        var document = new KnowledgeDocument(tenantId, "handbook.pdf", contentHash);
        db.KnowledgeDocuments.Add(document);
        await db.SaveChangesAsync();
        return document;
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task Chunk_WrittenByTenantA_IsReadableByA_AndInvisibleToB()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var document = await SeedDocumentAsync(tenantA, $"hash-{Guid.NewGuid():N}");

        await using (var dbA = fixture.CreateTenantContext(tenantA))
        {
            var writer = new KnowledgeChunkWriter(
                dbA, new FixedTenantContext(tenantA), NullLogger<KnowledgeChunkWriter>.Instance);

            await writer.WriteChunksAsync(
                document.Id,
                documentVersion: 1,
                [new KnowledgeChunkWrite("chunk text", Ordinal: 0, TokenCount: 3, RandomEmbedding(1024))],
                CancellationToken.None);
        }

        // Readable by tenant A (fresh context — nothing from the change tracker).
        await using (var dbA = fixture.CreateTenantContext(tenantA))
        {
            var chunk = await dbA.KnowledgeChunks.SingleAsync(c => c.DocumentId == document.Id);
            chunk.Content.Should().Be("chunk text");
            chunk.Ordinal.Should().Be(0);
            chunk.TokenCount.Should().Be(3);
            chunk.DocumentVersion.Should().Be(1);
            chunk.TenantId.Should().Be(tenantA);
        }

        // The stored vector really is 1024-dim (asserted at the DB, not through EF).
        await using (var conn = new NpgsqlConnection(fixture.SuperuserConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT vector_dims(embedding) FROM knowledge_chunks WHERE knowledge_document_id = @doc", conn);
            cmd.Parameters.AddWithValue("doc", document.Id);
            var dims = (int)(await cmd.ExecuteScalarAsync())!;
            dims.Should().Be(1024);
        }

        // Invisible to tenant B — RLS, not application filtering, hides the rows.
        await using (var dbB = fixture.CreateTenantContext(tenantB))
        {
            (await dbB.KnowledgeDocuments.AnyAsync(d => d.Id == document.Id)).Should().BeFalse();
            (await dbB.KnowledgeChunks.AnyAsync(c => c.DocumentId == document.Id)).Should().BeFalse();
        }
    }

    [Fact]
    public async Task WriteChunksAsync_ReRunForSameVersion_ReplacesRatherThanDuplicates()
    {
        var tenantId = Guid.NewGuid();
        var document = await SeedDocumentAsync(tenantId, $"hash-{Guid.NewGuid():N}");

        await using (var db = fixture.CreateTenantContext(tenantId))
        {
            var writer = new KnowledgeChunkWriter(
                db, new FixedTenantContext(tenantId), NullLogger<KnowledgeChunkWriter>.Instance);

            await writer.WriteChunksAsync(
                document.Id,
                documentVersion: 1,
                [
                    new KnowledgeChunkWrite("first pass chunk 0", 0, 4, RandomEmbedding(1024)),
                    new KnowledgeChunkWrite("first pass chunk 1", 1, 4, RandomEmbedding(1024)),
                ],
                CancellationToken.None);
        }

        // Re-run for the SAME document+version with a different chunk set — must replace, not append.
        await using (var db = fixture.CreateTenantContext(tenantId))
        {
            var writer = new KnowledgeChunkWriter(
                db, new FixedTenantContext(tenantId), NullLogger<KnowledgeChunkWriter>.Instance);

            await writer.WriteChunksAsync(
                document.Id,
                documentVersion: 1,
                [new KnowledgeChunkWrite("second pass chunk 0", 0, 4, RandomEmbedding(1024))],
                CancellationToken.None);
        }

        await using var verify = fixture.CreateTenantContext(tenantId);
        var chunks = await verify.KnowledgeChunks.Where(c => c.DocumentId == document.Id).ToListAsync();
        chunks.Should().HaveCount(1);
        chunks[0].Content.Should().Be("second pass chunk 0");
    }

    [Fact]
    public async Task Writer_Rejects_1536DimensionEmbedding()
    {
        var tenantId = Guid.NewGuid();
        var document = await SeedDocumentAsync(tenantId, $"hash-{Guid.NewGuid():N}");

        await using var db = fixture.CreateTenantContext(tenantId);
        var writer = new KnowledgeChunkWriter(
            db, new FixedTenantContext(tenantId), NullLogger<KnowledgeChunkWriter>.Instance);

        var act = () => writer.WriteChunksAsync(
            document.Id,
            documentVersion: 1,
            [new KnowledgeChunkWrite("chunk text", 0, 3, RandomEmbedding(1536))],
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*1536*1024*");

        // Nothing reached the database.
        await using var verify = fixture.CreateTenantContext(tenantId);
        (await verify.KnowledgeChunks.AnyAsync(c => c.DocumentId == document.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Database_Rejects_1536DimensionVector_EvenWithoutTheWriterGuard()
    {
        var tenantId = Guid.NewGuid();
        var document = await SeedDocumentAsync(tenantId, $"hash-{Guid.NewGuid():N}");

        var oversized = "[" + string.Join(",", Enumerable.Repeat("0", 1536)) + "]";

        await using var conn = new NpgsqlConnection(fixture.SuperuserConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $@"INSERT INTO knowledge_chunks (id, knowledge_document_id, tenant_id, content, embedding)
               VALUES (gen_random_uuid(), @doc, @tenant, 'x', '{oversized}'::vector)", conn);
        cmd.Parameters.AddWithValue("doc", document.Id);
        cmd.Parameters.AddWithValue("tenant", tenantId);

        var act = () => cmd.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.Message.Should().Contain("1024");
    }
}
