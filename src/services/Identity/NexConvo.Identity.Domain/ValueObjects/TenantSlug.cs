using System.Text.RegularExpressions;
using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Identity.Domain.ValueObjects;

/// <summary>A URL-safe tenant identifier: lower-case alphanumerics with single internal hyphens, 3–40 chars.</summary>
public sealed partial record TenantSlug
{
    public string Value { get; }

    private TenantSlug(string value) => Value = value;

    public static TenantSlug Create(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new DomainException("Tenant slug is required.");
        }

        var normalized = input.Trim().ToLowerInvariant();
        if (normalized.Length is < 3 or > 40 || !Pattern().IsMatch(normalized))
        {
            throw new DomainException("Tenant slug must be 3–40 chars: lower-case letters, digits, single hyphens.");
        }

        return new TenantSlug(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9](-?[a-z0-9])*$")]
    private static partial Regex Pattern();
}
