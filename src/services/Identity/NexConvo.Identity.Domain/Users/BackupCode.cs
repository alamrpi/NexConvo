namespace NexConvo.Identity.Domain.Users;

/// <summary>A one-time 2FA recovery code. Only its SHA-256 hash is stored; the plaintext is shown once.</summary>
public sealed record BackupCode(string Hash, DateTimeOffset? UsedAt = null);
