using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexConvo.Identity.Domain.Authentication;
using NexConvo.Identity.Domain.Invitations;
using NexConvo.Identity.Domain.Roles;
using NexConvo.Identity.Domain.Tenants;
using NexConvo.Identity.Domain.Users;
using NexConvo.Identity.Domain.ValueObjects;
using NexConvo.Identity.Domain.WorkspaceSettings;
using NexConvo.Identity.Infrastructure.Audit;

namespace NexConvo.Identity.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants"); // registry root — intentionally NOT RLS-scoped
        b.HasKey(t => t.Id);
        b.Ignore(t => t.DomainEvents);
        b.Property(t => t.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(t => t.Slug).HasColumnName("slug").HasMaxLength(40).IsRequired()
            .HasConversion(s => s.Value, v => TenantSlug.Create(v));
        b.Property(t => t.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        b.Property(t => t.CreatedAt).HasColumnName("created_at");
        b.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        b.Property(t => t.CreatedByUserId).HasColumnName("created_by_user_id");
        b.HasIndex(t => t.Slug).IsUnique();
    }
}

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(u => u.Id);
        b.Ignore(u => u.DomainEvents);
        b.Ignore(u => u.IsActive);
        b.Property(u => u.TenantId).HasColumnName("tenant_id");
        b.Property(u => u.Email).HasColumnName("email").HasMaxLength(320).IsRequired()
            .HasConversion(e => e.Value, v => Email.Create(v));
        b.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
        b.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        b.Property(u => u.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        b.Property(u => u.EmailVerifiedAt).HasColumnName("email_verified_at");
        b.Property(u => u.FailedLoginCount).HasColumnName("failed_login_count").HasDefaultValue(0);
        b.Property(u => u.LockoutEndsAt).HasColumnName("lockout_ends_at");
        b.Property(u => u.CreatedAt).HasColumnName("created_at");
        b.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        b.Property(u => u.CreatedByUserId).HasColumnName("created_by_user_id");
        b.Ignore(u => u.IsEmailVerified);
        b.HasMany(u => u.Roles).WithOne().HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(u => u.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("user_roles");
        b.HasKey(ur => ur.Id);
        b.Property(ur => ur.TenantId).HasColumnName("tenant_id");
        b.Property(ur => ur.UserId).HasColumnName("user_id");
        b.Property(ur => ur.RoleId).HasColumnName("role_id");
        b.HasIndex(ur => new { ur.TenantId, ur.UserId, ur.RoleId }).IsUnique();
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles");
        b.HasKey(r => r.Id);
        b.Ignore(r => r.DomainEvents);
        b.Ignore(r => r.PermissionKeys);
        b.Ignore(r => r.GrantsAll);
        b.Property(r => r.TenantId).HasColumnName("tenant_id");
        b.Property(r => r.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        b.Property(r => r.IsSystem).HasColumnName("is_system").HasDefaultValue(false);
        b.Property(r => r.CreatedAt).HasColumnName("created_at");
        b.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        b.Property(r => r.CreatedByUserId).HasColumnName("created_by_user_id");

        // Permissions stored as JSONB (skill Standard 7) via the private backing field.
        var permissions = b.Property<List<string>>("_permissions")
            .HasColumnName("permissions")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());
        permissions.Metadata.SetValueComparer(new ValueComparer<List<string>>(
            (a, c) => (a ?? new List<string>()).SequenceEqual(c ?? new List<string>()),
            v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode(StringComparison.Ordinal))),
            v => v.ToList()));

        b.HasIndex(r => new { r.TenantId, r.Name }).IsUnique();
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasKey(t => t.Id);
        b.Ignore(t => t.DomainEvents);
        b.Property(t => t.TenantId).HasColumnName("tenant_id");
        b.Property(t => t.UserId).HasColumnName("user_id");
        b.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        b.Property(t => t.ExpiresAt).HasColumnName("expires_at");
        b.Property(t => t.RevokedAt).HasColumnName("revoked_at");
        b.Property(t => t.ReplacedByHash).HasColumnName("replaced_by_hash").HasMaxLength(128);
        b.Property(t => t.CreatedAt).HasColumnName("created_at");
        b.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        b.Property(t => t.CreatedByUserId).HasColumnName("created_by_user_id");
        b.HasIndex(t => new { t.TenantId, t.TokenHash }).IsUnique();
    }
}

public sealed class OneTimeTokenConfiguration : IEntityTypeConfiguration<OneTimeToken>
{
    public void Configure(EntityTypeBuilder<OneTimeToken> b)
    {
        b.ToTable("one_time_tokens");
        b.HasKey(x => x.Id);
        b.Ignore(x => x.DomainEvents);
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Purpose).HasColumnName("purpose").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        b.Property(x => x.ConsumedAt).HasColumnName("consumed_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        b.HasIndex(x => new { x.TenantId, x.TokenHash }).IsUnique();
    }
}

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> b)
    {
        b.ToTable("invitations");
        b.HasKey(x => x.Id);
        b.Ignore(x => x.DomainEvents);
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(320).IsRequired()
            .HasConversion(e => e.Value, v => Email.Create(v));
        b.Property(x => x.RoleId).HasColumnName("role_id");
        b.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        b.Property(x => x.AcceptedAt).HasColumnName("accepted_at");
        b.Property(x => x.InvitedByUserId).HasColumnName("invited_by_user_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        b.HasIndex(x => new { x.TenantId, x.TokenHash }).IsUnique();
    }
}

public sealed class WorkspaceEmailSettingsConfiguration : IEntityTypeConfiguration<WorkspaceEmailSettings>
{
    public void Configure(EntityTypeBuilder<WorkspaceEmailSettings> b)
    {
        b.ToTable("workspace_email_settings");
        b.HasKey(x => x.Id);
        b.Ignore(x => x.DomainEvents);
        b.Ignore(x => x.HasSecret);
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Provider).HasColumnName("provider").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.FromName).HasColumnName("from_name").HasMaxLength(200);
        b.Property(x => x.FromAddress).HasColumnName("from_address").HasMaxLength(320);
        b.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        b.Property(x => x.SmtpHost).HasColumnName("smtp_host").HasMaxLength(255);
        b.Property(x => x.SmtpPort).HasColumnName("smtp_port");
        b.Property(x => x.SmtpUsername).HasColumnName("smtp_username").HasMaxLength(255);
        b.Property(x => x.SmtpUseSsl).HasColumnName("smtp_use_ssl");
        b.Property(x => x.EncryptedSecret).HasColumnName("encrypted_secret");
        b.Property(x => x.LastTestedAt).HasColumnName("last_tested_at");
        b.Property(x => x.LastTestSucceeded).HasColumnName("last_test_succeeded");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        b.HasIndex(x => x.TenantId).IsUnique(); // one settings row per tenant
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.HasKey(a => a.Id);
        b.Property(a => a.TenantId).HasColumnName("tenant_id");
        b.Property(a => a.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        b.Property(a => a.UserId).HasColumnName("user_id");
        b.Property(a => a.Detail).HasColumnName("detail").HasMaxLength(500);
        b.Property(a => a.OccurredAt).HasColumnName("occurred_at");
        b.HasIndex(a => new { a.TenantId, a.OccurredAt });
    }
}
