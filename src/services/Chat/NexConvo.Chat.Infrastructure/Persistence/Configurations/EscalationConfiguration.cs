using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexConvo.Chat.Domain.Entities;

namespace NexConvo.Chat.Infrastructure.Persistence.Configurations;

public sealed class EscalationConfiguration : IEntityTypeConfiguration<Escalation>
{
    public void Configure(EntityTypeBuilder<Escalation> builder)
    {
        builder.ToTable("escalations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Optimistic concurrency (Standard 16): PostgreSQL xmin system column.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.ConversationId)
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(e => e.Reason)
            .HasColumnName("reason")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(e => e.RaisedAt)
            .HasColumnName("raised_at")
            .IsRequired();

        builder.Property(e => e.AcceptedByUserId)
            .HasColumnName("accepted_by_user_id");

        builder.Property(e => e.AcceptedAt)
            .HasColumnName("accepted_at");

        builder.Property(e => e.ResolvedAt)
            .HasColumnName("resolved_at");

        builder.HasIndex(e => new { e.TenantId, e.ConversationId })
            .HasDatabaseName("ix_escalations_tenant_conversation");

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(e => e.ConversationId);
    }
}
