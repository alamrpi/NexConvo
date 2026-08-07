using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Chat.Domain.Entities;

namespace NexConvo.Chat.Infrastructure.Persistence.Configurations;

public sealed class ChannelConnectionConfiguration : IEntityTypeConfiguration<ChannelConnection>
{
    public void Configure(EntityTypeBuilder<ChannelConnection> builder)
    {
        builder.ToTable("channel_connections");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Optimistic concurrency (Standard 16): PostgreSQL xmin system column.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(x => x.Channel)
            .HasColumnName("channel")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(x => x.ExternalAccountId)
            .HasColumnName("external_account_id")
            .HasMaxLength(200);

        builder.Property(x => x.AccountName)
            .HasColumnName("display_name")
            .HasMaxLength(200);

        builder.Property(x => x.EncryptedAccessToken)
            .HasColumnName("encrypted_access_token")
            .IsRequired();

        builder.Property(x => x.EncryptedAppSecret)
            .HasColumnName("encrypted_app_secret");

        builder.Property(x => x.VerifyToken)
            .HasColumnName("verify_token")
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .HasColumnName("created_by_user_id");

        // Connection health tracking (Task 2 of channel-connection-health slice).
        builder.Property(x => x.LastTestStatus)
            .HasColumnName("last_test_status")
            .HasConversion<int>()
            .HasDefaultValue(ConnectionStatus.Untested);

        builder.Property(x => x.LastTestError)
            .HasColumnName("last_test_error")
            .HasMaxLength(1000);

        builder.Property(x => x.LastTestedAt)
            .HasColumnName("last_tested_at");

        builder.Property(x => x.LastTestLatencyMs)
            .HasColumnName("last_test_latency_ms");

        // Tenant lookup index.
        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("idx_channel_connections_tenant_id");

        // Filtered unique index: only one active connection per (tenant, channel, external_account).
        builder.HasIndex(x => new { x.TenantId, x.Channel, x.ExternalAccountId })
            .IsUnique()
            .HasFilter("is_active = true")
            .HasDatabaseName("idx_channel_connections_unique_active");
    }
}
