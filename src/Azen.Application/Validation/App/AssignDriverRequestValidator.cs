using Azen.Application.DTOs.App;
using Azen.Application.Validation.Common;
using FluentValidation;

namespace Azen.Application.Validation.App;

public class AssignDriverRequestValidator : AbstractValidator<AssignDriverRequest>
{
    public AssignDriverRequestValidator()
    {
        RuleFor(x => x)
            .Must(HaveExactlyOneAssignmentMode)
            .WithMessage("Provide either memberId (in-system) OR name + phone (external), not both.")
            .WithName("assignment");

        RuleFor(x => x.Name)
            .MaximumLength(200)
            .When(x => x.Name != null);

        RuleFor(x => x.Phone!)
            .PhoneE164()
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.VehicleNumber)
            .MaximumLength(50)
            .When(x => x.VehicleNumber != null);
    }

    private static bool HaveExactlyOneAssignmentMode(AssignDriverRequest r)
    {
        var inSystem = r.MemberId.HasValue;
        var external = !string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(r.Phone);
        return inSystem ^ external;
    }
}
