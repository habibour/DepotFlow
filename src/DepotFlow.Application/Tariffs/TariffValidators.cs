using FluentValidation;

namespace DepotFlow.Application.Tariffs;

/// <summary>Request shape only. The tier rules (gaps, overlaps, open-ended last tier) live in <c>Tariff.Validate()</c>.</summary>
public sealed class CreateTariffRequestValidator : AbstractValidator<CreateTariffRequest>
{
    public CreateTariffRequestValidator()
    {
        RuleFor(x => x.SizeFeet).NotNull().Must(s => s is 20 or 40).WithMessage("'Size Feet' must be 20 or 40.");
        RuleFor(x => x.StrategyKey).NotEmpty().MaximumLength(20);
        RuleFor(x => x.FreeDays).NotNull();
        RuleFor(x => x.Tiers).NotNull().NotEmpty();
        RuleForEach(x => x.Tiers).ChildRules(tier =>
        {
            tier.RuleFor(t => t.FromDay).NotNull();
            tier.RuleFor(t => t.RatePerDay).NotNull();
        });
    }
}

public sealed class UpdateTariffRequestValidator : AbstractValidator<UpdateTariffRequest>
{
    public UpdateTariffRequestValidator()
    {
        RuleFor(x => x.IsActive)
            .NotNull()
            .Must(active => active == false)
            .WithMessage("A tariff can only be deactivated. To change prices, create a new tariff.");
    }
}
