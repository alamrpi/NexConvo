using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexConvo.Knowledge.Domain.Entities;

namespace NexConvo.Knowledge.Infrastructure.Persistence.Configurations;

// Note: source_type, title, failure_reason, embedding_model, embedding_dimensions and
// chunk_count columns were created (with defaults) by the InitialKnowledgeSchema migration
// but left unmapped until this slice added the corresponding KnowledgeDocument properties.

public sealed class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> builder)
    {
        builder.ToTable("knowledge_documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(d => d.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(d => d.FileName)
            .HasColumnName("original_file_name")
            .HasMaxLength(500);

        builder.Property(d => d.ContentHash)
            .HasColumnName("content_hash")
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(d => d.Status)
            .HasColumnName("ingestion_status")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(d => d.SourceType)
            .HasColumnName("source_type")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(d => d.Title)
            .HasColumnName("title")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.SourceUrl)
            .HasColumnName("source_url");

        builder.Property(d => d.S3ObjectKey)
            .HasColumnName("s3_object_key")
            .HasMaxLength(1000);

        builder.Property(d => d.FailureReason)
            .HasColumnName("failure_reason");

        builder.Property(d => d.EmbeddingModel)
            .HasColumnName("embedding_model")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.EmbeddingDimensions)
            .HasColumnName("embedding_dimensions")
            .IsRequired();

        builder.Property(d => d.ChunkCount)
            .HasColumnName("chunk_count")
            .IsRequired();

        builder.Property(d => d.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(d => d.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(d => d.CreatedByUserId)
            .HasColumnName("created_by_user_id");

        // Optimistic concurrency via PostgreSQL system column xmin (Standard 16)
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Dedup guard: same content cannot be re-uploaded for the same tenant. Partial (WHERE
        // is_active) so a soft-deleted document's hash can be reused — otherwise re-uploading
        // identical content after deletion hits this constraint (unhandled 500) even though
        // UploadKnowledgeDocumentCommandHandler's own dedup check already ignores inactive rows.
        builder.HasIndex(d => new { d.TenantId, d.ContentHash })
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName("idx_knowledge_documents_content_hash");

        builder.HasIndex(d => d.TenantId)
            .HasDatabaseName("idx_knowledge_documents_tenant");
    }
}
