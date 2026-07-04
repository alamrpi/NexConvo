using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexConvo.Chat.Domain.Entities;

namespace NexConvo.Chat.Infrastructure.Persistence.Configurations;

public sealed class ChatAuditLogConfiguration : IEntityTypeConfiguration<ChatAuditLog>
{
    public void Configure(EntityTypeBuilder<ChatAuditLog> builder)
    {
        builder.ToTable("chat_audit_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Action).HasColumnName("action").IsRequired();
        builder.Property(x => x.Detail).HasColumnName("detail");
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();

        builder.HasIndex(x => x.TenantId).HasDatabaseName("idx_chat_audit_logs_tenant_id");
        builder.HasIndex(x => x.OccurredAt).HasDatabaseName("idx_chat_audit_logs_occurred_at");
    }
}
