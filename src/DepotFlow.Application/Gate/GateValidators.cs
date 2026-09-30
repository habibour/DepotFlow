using FluentValidation;

namespace DepotFlow.Application.Gate;

public sealed class GateInRequestValidator : AbstractValidator<GateInRequest>
{
    public GateInRequestValidator()
    {
        RuleFor(x => x.ContainerNumber).NotEmpty();
        RuleFor(x => x.SizeFeet).NotNull().Must(s => s is 20 or 40).WithMessage("'Size Feet' must be 20 or 40.");
        RuleFor(x => x.ShippingLineId).NotNull().GreaterThan(0);
        RuleFor(x => x.TruckNumber).NotEmpty().MaximumLength(GateLimits.TruckNumber);
        RuleFor(x => x.SealNumber).NotEmpty().MaximumLength(GateLimits.SealNumber);
        RuleFor(x => x.DamageNotes).MaximumLength(GateLimits.DamageNotes);
    }
}

public sealed class GateOutRequestValidator : AbstractValidator<GateOutRequest>
{
    public GateOutRequestValidator()
    {
        RuleFor(x => x.TruckNumber).NotEmpty().MaximumLength(GateLimits.TruckNumber);
        RuleFor(x => x.DamageNotes).MaximumLength(GateLimits.DamageNotes);
    }
}
