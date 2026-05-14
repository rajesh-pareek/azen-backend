using Azen.Application.DTOs.App;
using FluentValidation;

namespace Azen.Application.Validation.App;

public class UpdateStatusRequestValidator : AbstractValidator<UpdateStatusRequest>
{
    private static readonly HashSet<string> AllowedStatuses = new()
    {
        "created", "assigned", "pod_uploaded", "shared"
    };

    public UpdateStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => AllowedStatuses.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", AllowedStatuses)}.");
    }
}
