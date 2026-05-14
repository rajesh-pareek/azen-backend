using Azen.Application.DTOs.App;
using Azen.Application.Validation.Common;
using FluentValidation;

namespace Azen.Application.Validation.App;

public class UpdateShipmentRequestValidator : AbstractValidator<UpdateShipmentRequest>
{
    public UpdateShipmentRequestValidator()
    {
        RuleFor(x => x.ConsignorName).MaximumLength(200);
        RuleFor(x => x.ConsignorPhone).PhoneE164Optional();

        RuleFor(x => x.ConsigneeName).MaximumLength(200);
        RuleFor(x => x.ConsigneePhone).PhoneE164Optional();

        RuleFor(x => x.GoodsDescription).MaximumLength(500);
        RuleFor(x => x.VehicleNumber).MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
