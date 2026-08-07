using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Integrations.Domain.Entities;

namespace NexConvo.Integrations.Infrastructure.Persistence.Configurations;

public class WorkspaceAiConfigConfiguration : IEntityTypeConfiguration<WorkspaceAiConfig>
{
    public void Configure(EntityTypeBuilder<WorkspaceAiConfig> builder)
    {
        builder.ToTable("WorkspaceAiConfigs");

        builder.HasKey(x => x.Id);

        // Optimistic concurrency (skill Standard 16): map PostgreSQL's system xmin column as a
        // shadow concurrency token so two concurrent saves can't silently last-writer-win — the
        // loser gets a DbUpdateConcurrencyException, surfaced as 409 by UseNexConvoExceptionHandling.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(x => x.Provider)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.EncryptedApiKey)
            .IsRequired();

        builder.Property(x => x.BaseUrl)
            .HasMaxLength(500);

        builder.Property(x => x.DefaultModel)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Parameters)
            .HasColumnType("jsonb");

        // RLS configuration constraint for PostgreSQL (to be managed at migration)
        builder.HasIndex(x => x.TenantId);
        
        // Ensure only one active provider per tenant
        builder.HasIndex(x => new { x.TenantId, x.IsActive })
            .IsUnique()
            .HasFilter("\"IsActive\" = true");

        // Connection health tracking (Task 4).
        builder.Property(x => x.LastTestStatus)
            .HasConversion<int>()
            .HasDefaultValue(ConnectionStatus.Untested);

        builder.Property(x => x.LastTestError)
            .HasMaxLength(1000);

        builder.Property(x => x.LastTestedAt);

        builder.Property(x => x.LastTestLatencyMs);
    }
}
