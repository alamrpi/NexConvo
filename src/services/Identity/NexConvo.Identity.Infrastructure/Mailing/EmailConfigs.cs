namespace NexConvo.Identity.Infrastructure.Mailing;

/// <summary>Resolved SMTP transport config (secret already decrypted) for one send.</summary>
public sealed record SmtpEmailConfig(
    string Host,
    int Port,
    bool UseSsl,
    string? Username,
    string? Password,
    string FromAddress,
    string FromName);

/// <summary>Resolved Resend transport config (API key already decrypted) for one send.</summary>
public sealed record ResendEmailConfig(string ApiKey, string FromAddress, string FromName);
