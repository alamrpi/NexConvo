using System.Net;
using System.Text.Json;
using FluentValidation;
using NexConvo.Contracts.Enums;

namespace NexConvo.Integrations.Application.Features.AiConfig.Commands;

/// <summary>
/// Validates the AI config upsert (skill Standard 3). Notably: restricts the provider to the ones
/// with a working implementation, and hardens <c>BaseUrl</c> against SSRF — the decrypted API key is
/// later sent to that URL, so an unvalidated host could exfiltrate it or hit cloud metadata.
/// </summary>
public sealed class SaveAiConfigCommandValidator : AbstractValidator<SaveAiConfigCommand>
{
    public SaveAiConfigCommandValidator()
    {
        RuleFor(x => x.Provider)
            .IsInEnum()
            .WithMessage("Provider must be a valid AI provider.");

        RuleFor(x => x.DefaultModel)
            .NotEmpty().WithMessage("Default model is required.")
            .MaximumLength(100);

        // BaseUrl is optional, but when supplied must be an absolute https URL to a public host.
        When(x => !string.IsNullOrWhiteSpace(x.BaseUrl), () =>
        {
            RuleFor(x => x.BaseUrl!)
                .MaximumLength(500)
                .Must(BeAPublicHttpsUrl)
                .WithMessage("Base URL must be an absolute https:// URL to a public host.");
        });

        // Parameters, when supplied, must be a JSON object (it is persisted as jsonb).
        When(x => !string.IsNullOrWhiteSpace(x.Parameters), () =>
        {
            RuleFor(x => x.Parameters!)
                .Must(BeAJsonObject)
                .WithMessage("Parameters must be a valid JSON object.");
        });
    }

    private static bool BeAPublicHttpsUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && uri.Scheme == Uri.UriSchemeHttps
           && !IsPrivateOrLoopbackHost(uri);

    private static bool IsPrivateOrLoopbackHost(Uri uri)
    {
        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(uri.Host, out var ip))
        {
            return false; // a DNS name — allowed (resolution-time SSRF is out of scope here)
        }

        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        var b = ip.GetAddressBytes();
        if (b.Length != 4)
        {
            return false;
        }

        // RFC1918 private ranges + 169.254/16 link-local (incl. 169.254.169.254 cloud metadata).
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254);
    }

    private static bool BeAJsonObject(string value)
    {
        try
        {
            using var doc = JsonDocument.Parse(value);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
