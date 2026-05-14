using System.Text.RegularExpressions;
using FluentValidation;

namespace Azen.Application.Validation.Common;

/// <summary>
/// Reusable rule builder extensions for phone numbers.
/// E.164 format: leading '+', then a non-zero digit, then 1-14 more digits.
/// Total length 2-15 chars. No spaces, dashes, or parentheses allowed.
/// Examples: +919876543210, +14155552671
/// </summary>
public static class PhoneNumberRules
{
    // Strict E.164. Max 15 digits total (excluding the '+').
    private static readonly Regex E164 = new(
        @"^\+[1-9]\d{1,14}$",
        RegexOptions.Compiled);

    /// <summary>
    /// Required E.164 phone number. Use on string properties.
    /// </summary>
    public static IRuleBuilderOptions<T, string> PhoneE164<T>(
        this IRuleBuilder<T, string> rule)
    {
        return rule
            .NotEmpty().WithMessage("Phone is required.")
            .Matches(E164).WithMessage(
                "Phone must be E.164 format: '+' followed by country code and digits, no spaces or dashes (e.g. +919876543210).");
    }

    /// <summary>
    /// Optional E.164 phone number. Null or empty passes. Non-empty is validated.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> PhoneE164Optional<T>(
        this IRuleBuilder<T, string?> rule)
    {
        return rule
            .Must(p => string.IsNullOrEmpty(p) || E164.IsMatch(p!))
            .WithMessage(
                "Phone must be E.164 format: '+' followed by country code and digits, no spaces or dashes (e.g. +919876543210).");
    }
}
