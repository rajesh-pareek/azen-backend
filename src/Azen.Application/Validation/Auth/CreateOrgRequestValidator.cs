using System.Text.RegularExpressions;
using Azen.Application.DTOs.Auth;
using Azen.Application.Validation.Common;
using FluentValidation;

namespace Azen.Application.Validation.Auth;

public class CreateOrgRequestValidator : AbstractValidator<CreateOrgRequest>
{
    // lowercase alphanumeric + hyphens; cannot start or end with hyphen; 3-60 chars
    private static readonly Regex SlugRegex = new(
        @"^[a-z0-9](?:[a-z0-9-]{1,58}[a-z0-9])?$",
        RegexOptions.Compiled);

    public CreateOrgRequestValidator()
    {
        RuleFor(x => x.Phone).PhoneE164();

        RuleFor(x => x.AuthCode)
            .NotEmpty().WithMessage("AuthCode is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Org name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required.")
            .Matches(SlugRegex).WithMessage(
                "Slug must be 3-60 chars, lowercase letters, digits and hyphens only, no leading/trailing hyphen.");
    }
}
