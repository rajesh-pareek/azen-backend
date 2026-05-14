using Azen.Application.DTOs.Auth;
using Azen.Application.Validation.Common;
using FluentValidation;

namespace Azen.Application.Validation.Auth;

public class TokenRequestValidator : AbstractValidator<TokenRequest>
{
    public TokenRequestValidator()
    {
        RuleFor(x => x.OrgId)
            .NotEqual(Guid.Empty).WithMessage("OrgId is required.");

        RuleFor(x => x.Phone).PhoneE164();

        RuleFor(x => x.AuthCode)
            .NotEmpty().WithMessage("AuthCode is required.");
    }
}
