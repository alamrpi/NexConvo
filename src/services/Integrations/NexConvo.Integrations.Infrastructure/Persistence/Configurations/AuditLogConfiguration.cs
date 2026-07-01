using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexConvo.Integrations.Domain.Entities;

namespace NexConvo.Integrations.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Detail)
            .HasMaxLength(2000);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.OccurredAt);
    }
}
