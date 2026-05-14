using Azen.Application.DTOs.App;
using Azen.Application.Validation.Common;
using FluentValidation;

namespace Azen.Application.Validation.App;

public class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    private static readonly HashSet<string> AllowedRoles = new()
    {
        "transporter", "fleet_owner", "driver"
    };

    public InviteMemberRequestValidator()
    {
        RuleFor(x => x.Phone).PhoneE164();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(r => AllowedRoles.Contains(r))
            .WithMessage($"Role must be one of: {string.Join(", ", AllowedRoles)}.");
    }
}
