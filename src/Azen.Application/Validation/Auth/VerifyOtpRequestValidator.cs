using Azen.Application.DTOs.Auth;
using Azen.Application.Validation.Common;
using FluentValidation;

namespace Azen.Application.Validation.Auth;

public class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequest>
{
    public VerifyOtpRequestValidator()
    {
        RuleFor(x => x.Phone).PhoneE164();

        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage("OTP is required.")
            .Matches(@"^\d{6}$").WithMessage("OTP must be exactly 6 digits.");
    }
}
