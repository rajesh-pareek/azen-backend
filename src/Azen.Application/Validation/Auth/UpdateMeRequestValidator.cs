using Azen.Application.DTOs.Auth;
using FluentValidation;

namespace Azen.Application.Validation.Auth;

public class UpdateMeRequestValidator : AbstractValidator<UpdateMeRequest>
{
    public UpdateMeRequestValidator()
    {
        // Name is optional. When provided, must be non-blank and within length limit.
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name cannot be blank when provided.")
            .MaximumLength(200)
            .When(x => x.Name != null);
    }
}
