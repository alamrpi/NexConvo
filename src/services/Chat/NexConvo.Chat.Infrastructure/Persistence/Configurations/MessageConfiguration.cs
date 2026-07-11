using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexConvo.Chat.Domain.Entities;

namespace NexConvo.Chat.Infrastructure.Persistence.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Optimistic concurrency (Standard 16): PostgreSQL xmin system column.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(m => m.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(m => m.ConversationId)
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(m => m.Direction)
            .HasColumnName("direction")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(m => m.Body)
            .HasColumnName("body")
            .IsRequired();

        builder.Property(m => m.ProviderMessageId)
            .HasColumnName("provider_message_id");

        builder.Property(m => m.DeliveryStatus)
            .HasColumnName("delivery_status")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(m => m.Confidence)
            .HasColumnName("confidence");

        builder.Property(m => m.StructuredPayload)
            .HasColumnName("structured_payload")
            .HasColumnType("jsonb");

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.OwnsOne(m => m.Sender, s =>
        {
            s.Property(x => x.Role)
                .HasColumnName("sender_role")
                .HasConversion<short>()
                .IsRequired();

            s.Property(x => x.DisplayRef)
                .HasColumnName("sender_display_ref");

            s.Property(x => x.UserId)
                .HasColumnName("sender_user_id");
        });

        builder.HasIndex(m => new { m.TenantId, m.ConversationId })
            .HasDatabaseName("ix_messages_tenant_conversation");

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(m => m.ConversationId);
    }
}
