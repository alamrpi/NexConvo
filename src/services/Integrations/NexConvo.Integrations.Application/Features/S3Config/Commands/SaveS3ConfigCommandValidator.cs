using System.Net;
using FluentValidation;

namespace NexConvo.Integrations.Application.Features.S3Config.Commands;

public sealed class SaveS3ConfigCommandValidator : AbstractValidator<SaveS3ConfigCommand>
{
    private static readonly System.Text.RegularExpressions.Regex S3BucketNameRegex =
        new(@"^[a-z0-9][a-z0-9\-]{1,61}[a-z0-9]$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public SaveS3ConfigCommandValidator()
    {
        RuleFor(x => x.BucketName)
            .NotEmpty().WithMessage("Bucket name is required.")
            .MaximumLength(63)
            .Must(n => S3BucketNameRegex.IsMatch(n))
            .WithMessage("Bucket name must be 3–63 lowercase letters, numbers, or hyphens, and cannot start or end with a hyphen.");

        RuleFor(x => x.Region)
            .NotEmpty().WithMessage("Region is required.")
            .MaximumLength(50);

        When(x => !string.IsNullOrWhiteSpace(x.CustomEndpoint), () =>
        {
            RuleFor(x => x.CustomEndpoint!)
                .MaximumLength(500)
                .Must(BeAValidEndpointUrl)
                .WithMessage("Custom endpoint must be an absolute http:// or https:// URL.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.PathPrefix), () =>
        {
            RuleFor(x => x.PathPrefix!)
                .MaximumLength(200)
                .Must(p => !p.StartsWith('/'))
                .WithMessage("Path prefix must not start with a slash.")
                .Must(p => p.EndsWith('/'))
                .WithMessage("Path prefix must end with a slash, e.g. \"knowledge/\".");
        });
    }

    private static bool BeAValidEndpointUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
