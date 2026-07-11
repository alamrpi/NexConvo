using FluentValidation;
using NexConvo.Knowledge.Domain.Enums;

namespace NexConvo.Knowledge.Application.Features.KnowledgeBase.Commands;

/// <summary>
/// Source-type-conditional validation for <see cref="UploadKnowledgeDocumentCommand"/>.
/// Each <see cref="SourceType"/> carries its own required payload; all four share the
/// Title/ContentHash rules that apply regardless of source.
/// </summary>
public sealed class UploadKnowledgeDocumentCommandValidator : AbstractValidator<UploadKnowledgeDocumentCommand>
{
    public UploadKnowledgeDocumentCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(500);

        RuleFor(x => x.ContentHash)
            .NotEmpty().WithMessage("ContentHash is required.");

        When(x => x.SourceType == SourceType.File, () =>
        {
            RuleFor(x => x.FileName)
                .NotEmpty().WithMessage("FileName is required for a File upload.");

            RuleFor(x => x.FileStream)
                .NotNull().WithMessage("FileStream is required for a File upload.");
        });

        When(x => x.SourceType == SourceType.Url, () =>
        {
            RuleFor(x => x.SourceUrl)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("SourceUrl is required for a Url upload.")
                .Must(BeAWellFormedHttpsUrl)
                .WithMessage("SourceUrl must be a well-formed absolute https:// URL.");
        });

        When(x => x.SourceType == SourceType.Text, () =>
        {
            RuleFor(x => x.RawText)
                .NotEmpty().WithMessage("RawText is required for a Text upload.");
        });

        When(x => x.SourceType == SourceType.Faq, () =>
        {
            RuleFor(x => x.FaqPairs)
                .NotEmpty().WithMessage("At least one FAQ pair is required for a Faq upload.");

            RuleForEach(x => x.FaqPairs)
                .ChildRules(pair =>
                {
                    pair.RuleFor(p => p.Question).NotEmpty().WithMessage("Each FAQ pair requires a question.");
                    pair.RuleFor(p => p.Answer).NotEmpty().WithMessage("Each FAQ pair requires an answer.");
                })
                .When(x => x.FaqPairs is not null);
        });
    }

    private static bool BeAWellFormedHttpsUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && uri.Scheme == Uri.UriSchemeHttps;
}
