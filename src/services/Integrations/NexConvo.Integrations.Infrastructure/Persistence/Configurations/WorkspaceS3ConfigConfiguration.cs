using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexConvo.Integrations.Domain.Entities;

namespace NexConvo.Integrations.Infrastructure.Persistence.Configurations;

public class WorkspaceS3ConfigConfiguration : IEntityTypeConfiguration<WorkspaceS3Config>
{
    public void Configure(EntityTypeBuilder<WorkspaceS3Config> builder)
    {
        builder.ToTable("WorkspaceS3Configs");

        builder.HasKey(x => x.Id);

        // Optimistic concurrency token — PostgreSQL system column xmin (Standard 16).
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(x => x.BucketName)
            .IsRequired()
            .HasMaxLength(63);

        builder.Property(x => x.Region)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.EncryptedAccessKeyId)
            .IsRequired();

        builder.Property(x => x.EncryptedSecretAccessKey)
            .IsRequired();

        builder.Property(x => x.CustomEndpoint)
            .HasMaxLength(500);

        builder.Property(x => x.PathPrefix)
            .HasMaxLength(200);

        // One config per tenant (not keyed by provider type, unlike AiConfig).
        builder.HasIndex(x => x.TenantId)
            .IsUnique()
            .HasDatabaseName("IX_WorkspaceS3Configs_TenantId");
    }
}
