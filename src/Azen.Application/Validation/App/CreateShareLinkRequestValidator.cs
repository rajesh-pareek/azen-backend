using Azen.Application.DTOs.App;
using FluentValidation;

namespace Azen.Application.Validation.App;

public class CreateShareLinkRequestValidator : AbstractValidator<CreateShareLinkRequest>
{
    private static readonly HashSet<string> AllowedDocTypes = new()
    {
        "pod", "invoice", "lr", "weightbridge", "eway_bill", "consignment_note", "custom"
    };

    public CreateShareLinkRequestValidator()
    {
        RuleFor(x => x.VisibleDocTypes)
            .NotNull().WithMessage("visibleDocTypes is required.")
            .Must(list => list != null && list.Count > 0)
            .WithMessage("At least one doc type must be visible.")
            .Must(list => list == null || list.All(t => AllowedDocTypes.Contains(t)))
            .WithMessage($"Each doc type must be one of: {string.Join(", ", AllowedDocTypes)}.");

        RuleFor(x => x.ExpiresInDays)
            .InclusiveBetween(1, 90)
            .When(x => x.ExpiresInDays.HasValue)
            .WithMessage("expiresInDays must be between 1 and 90.");
    }
}
