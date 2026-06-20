using System.Text.RegularExpressions;
using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Identity.Domain.ValueObjects;

/// <summary>A validated, lower-cased email address.</summary>
public sealed partial record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new DomainException("Email is required.");
        }

        var normalized = input.Trim().ToLowerInvariant();
        if (!Pattern().IsMatch(normalized))
        {
            throw new DomainException("Email is not a valid address.");
        }

        return new Email(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Pattern();
}
