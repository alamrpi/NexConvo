using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexConvo.Chat.Domain.Entities;

namespace NexConvo.Chat.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceChatSettingsConfiguration : IEntityTypeConfiguration<WorkspaceChatSettings>
{
    public void Configure(EntityTypeBuilder<WorkspaceChatSettings> builder)
    {
        builder.ToTable("workspace_chat_settings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        builder.HasIndex(x => x.TenantId)
            .IsUnique()
            .HasDatabaseName("idx_workspace_chat_settings_tenant_id");

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(x => x.PrimaryProvider)
            .HasColumnName("primary_provider")
            .IsRequired();

        builder.Property(x => x.PrimaryModel)
            .HasColumnName("primary_model")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.FallbackProviders)
            .HasColumnName("fallback_providers")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValue("[]");

        builder.Property(x => x.SystemPromptOverride)
            .HasColumnName("system_prompt_override")
            .HasMaxLength(4000);

        builder.Property(x => x.HandoffConfidenceThreshold)
            .HasColumnName("handoff_confidence_threshold")
            .IsRequired()
            .HasDefaultValue(0.65);

        builder.Property(x => x.SentimentEscalationEnabled)
            .HasColumnName("sentiment_escalation_enabled")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.SentimentSensitivity)
            .HasColumnName("sentiment_sensitivity")
            .IsRequired();

        builder.Property(x => x.TriggerPhrases)
            .HasColumnName("trigger_phrases")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValue("[]");

        builder.Property(x => x.MaxUnansweredMessages)
            .HasColumnName("max_unanswered_messages")
            .IsRequired()
            .HasDefaultValue(3);

        builder.Property(x => x.PiiMaskingLevel)
            .HasColumnName("pii_masking_level")
            .IsRequired();

        builder.Property(x => x.DataRetentionDays)
            .HasColumnName("data_retention_days");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
    }
}
