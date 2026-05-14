using Azen.Application.DTOs.Auth;
using Azen.Application.Validation.Common;
using FluentValidation;

namespace Azen.Application.Validation.Auth;

public class SendOtpRequestValidator : AbstractValidator<SendOtpRequest>
{
    public SendOtpRequestValidator()
    {
        RuleFor(x => x.Phone).PhoneE164();
    }
}
