using Azen.Application.DTOs.App;
using Azen.Application.Validation.Common;
using FluentValidation;

namespace Azen.Application.Validation.App;

public class CreateShipmentRequestValidator : AbstractValidator<CreateShipmentRequest>
{
    public CreateShipmentRequestValidator()
    {
        RuleFor(x => x.ReferenceNumber)
            .MaximumLength(100);

        RuleFor(x => x.ConsignorName).MaximumLength(200);
        RuleFor(x => x.ConsignorPhone).PhoneE164Optional();

        RuleFor(x => x.ConsigneeName).MaximumLength(200);
        RuleFor(x => x.ConsigneePhone).PhoneE164Optional();

        RuleFor(x => x.GoodsDescription).MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
