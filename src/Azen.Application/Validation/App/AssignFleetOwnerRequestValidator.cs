using Azen.Application.DTOs.App;
using Azen.Application.Validation.Common;
using FluentValidation;

namespace Azen.Application.Validation.App;

public class AssignFleetOwnerRequestValidator : AbstractValidator<AssignFleetOwnerRequest>
{
    public AssignFleetOwnerRequestValidator()
    {
        // Either: MemberId (in-system) OR (Name + Phone) (external). Not both, not neither.
        RuleFor(x => x)
            .Must(HaveExactlyOneAssignmentMode)
            .WithMessage("Provide either memberId (in-system) OR name + phone (external), not both.")
            .WithName("assignment");

        // Per-field rules apply when the relevant fields are present.
        RuleFor(x => x.Name)
            .MaximumLength(200)
            .When(x => x.Name != null);

        RuleFor(x => x.Phone!)
            .PhoneE164()
            .When(x => !string.IsNullOrEmpty(x.Phone));
    }

    private static bool HaveExactlyOneAssignmentMode(AssignFleetOwnerRequest r)
    {
        var inSystem = r.MemberId.HasValue;
        var external = !string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(r.Phone);
        return inSystem ^ external;
    }
}
