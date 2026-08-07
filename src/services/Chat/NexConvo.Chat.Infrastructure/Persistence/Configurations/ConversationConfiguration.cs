using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexConvo.Chat.Domain.Entities;

namespace NexConvo.Chat.Infrastructure.Persistence.Configurations;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Optimistic concurrency (Standard 16): PostgreSQL xmin system column.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(c => c.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(c => c.State)
            .HasColumnName("state")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(c => c.ContactId)
            .HasColumnName("contact_id");

        builder.Property(c => c.AssignedAgentUserId)
            .HasColumnName("assigned_agent_user_id");

        builder.Property(c => c.LastInboundProviderMessageId)
            .HasColumnName("last_inbound_provider_message_id");

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(c => c.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.OwnsOne(c => c.Channel, ch =>
        {
            ch.Property(x => x.Channel)
                .HasColumnName("channel")
                .HasConversion<short>()
                .IsRequired();

            ch.Property(x => x.ExternalConversationId)
                .HasColumnName("external_conversation_id")
                .IsRequired();
        });

        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("ix_conversations_tenant_id");
    }
}
