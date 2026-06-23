using Microsoft.Extensions.Configuration;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Infrastructure.Common;

/// <summary>Builds absolute front-end links from the configured <c>App:BaseUrl</c>.</summary>
public sealed class AppLinkBuilder(IConfiguration configuration) : IAppLinkBuilder
{
    private string BaseUrl => (configuration["App:BaseUrl"] ?? "http://localhost:3003").TrimEnd('/');

    public string VerifyEmailLink(string token) => $"{BaseUrl}/verify-email?token={Uri.EscapeDataString(token)}";

    public string ResetPasswordLink(string token) => $"{BaseUrl}/reset-password?token={Uri.EscapeDataString(token)}";

    public string AcceptInvitationLink(string token) => $"{BaseUrl}/accept-invite?token={Uri.EscapeDataString(token)}";
}
