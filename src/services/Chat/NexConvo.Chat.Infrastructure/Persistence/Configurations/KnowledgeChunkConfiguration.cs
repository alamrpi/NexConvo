using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexConvo.Chat.Domain.Entities;
using Pgvector;

namespace NexConvo.Chat.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunk> builder)
    {
        builder.ToTable("knowledge_chunks");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(c => c.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(c => c.DocumentId)
            .HasColumnName("knowledge_document_id")
            .IsRequired();

        builder.Property(c => c.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(c => c.DocumentVersion)
            .HasColumnName("document_version")
            .IsRequired();

        builder.Property(c => c.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // knowledge_chunks has no updated_at or created_by_user_id columns — chunks are immutable after creation.
        builder.Ignore(c => c.UpdatedAt);
        builder.Ignore(c => c.CreatedByUserId);

        // Vector column via pgvector — shadow property (not on entity yet; used in Phase 2 embedding)
        builder.Property<Vector>("Embedding")
            .HasColumnName("embedding")
            .HasColumnType("vector(1536)");

        // FK to knowledge_documents — DocumentId maps to knowledge_document_id column.
        builder.HasOne<KnowledgeDocument>()
            .WithMany()
            .HasForeignKey(c => c.DocumentId)
            .HasConstraintName("knowledge_chunks_knowledge_document_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("idx_knowledge_chunks_tenant");

        builder.HasIndex(c => new { c.TenantId, c.DocumentId })
            .HasDatabaseName("idx_knowledge_chunks_document");
    }
}
