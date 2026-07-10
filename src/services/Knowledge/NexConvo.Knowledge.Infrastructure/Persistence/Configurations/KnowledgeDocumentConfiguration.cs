using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexConvo.Knowledge.Domain.Entities;

namespace NexConvo.Knowledge.Infrastructure.Persistence.Configurations;

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

        // Dedup guard: same content cannot be re-uploaded for the same tenant
        builder.HasIndex(d => new { d.TenantId, d.ContentHash })
            .IsUnique()
            .HasDatabaseName("idx_knowledge_documents_content_hash");

        builder.HasIndex(d => d.TenantId)
            .HasDatabaseName("idx_knowledge_documents_tenant");
    }
}
