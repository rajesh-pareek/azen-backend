using Azen.Api.DTOs;
using FluentValidation;

namespace Azen.Api.Validators;

public class UploadDocumentRequestValidator : AbstractValidator<UploadDocumentRequest>
{
    private static readonly HashSet<string> AllowedDocTypes = new()
    {
        "pod", "invoice", "lr", "weightbridge", "eway_bill", "consignment_note", "custom"
    };

    public UploadDocumentRequestValidator()
    {
        RuleFor(x => x.File)
            .NotNull().WithMessage("file is required.")
            .Must(f => f != null && f.Length > 0)
            .WithMessage("file cannot be empty.");

        RuleFor(x => x.DocType)
            .NotEmpty().WithMessage("docType is required.")
            .Must(t => AllowedDocTypes.Contains(t))
            .WithMessage($"docType must be one of: {string.Join(", ", AllowedDocTypes)}.");
    }
}
