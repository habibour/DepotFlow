using FluentValidation;

namespace DepotFlow.Application.Reports;

/// <summary>Shared date-range rules: both dates required, from not after to, and a maximum span in days.</summary>
internal static class RangeRules
{
    public const int DailyMovementsMaxDays = 366;
    public const int OtherReportsMaxDays = 1830;

    public static void Apply<T>(AbstractValidator<T> validator, Func<T, DateOnly?> from, Func<T, DateOnly?> to, int maxDays)
    {
        validator.RuleFor(x => from(x)).NotNull().WithName("From").WithMessage("'from' is required (yyyy-MM-dd).");
        validator.RuleFor(x => to(x)).NotNull().WithName("To").WithMessage("'to' is required (yyyy-MM-dd).");

        validator.RuleFor(x => x)
            .Must(x => from(x)!.Value <= to(x)!.Value)
            .WithName("To").WithMessage("'from' must not be after 'to'.")
            .When(x => from(x) is not null && to(x) is not null);

        validator.RuleFor(x => x)
            .Must(x => to(x)!.Value.DayNumber - from(x)!.Value.DayNumber + 1 <= maxDays)
            .WithName("To").WithMessage($"The range can be at most {maxDays} days.")
            .When(x => from(x) is not null && to(x) is not null && from(x)!.Value <= to(x)!.Value);
    }
}

public sealed class DailyMovementsRequestValidator : AbstractValidator<DailyMovementsRequest>
{
    public DailyMovementsRequestValidator() =>
        RangeRules.Apply(this, x => x.From, x => x.To, RangeRules.DailyMovementsMaxDays);
}

public sealed class DwellTimeRequestValidator : AbstractValidator<DwellTimeRequest>
{
    public DwellTimeRequestValidator() =>
        RangeRules.Apply(this, x => x.From, x => x.To, RangeRules.OtherReportsMaxDays);
}

public sealed class RevenueRequestValidator : AbstractValidator<RevenueRequest>
{
    public RevenueRequestValidator() =>
        RangeRules.Apply(this, x => x.From, x => x.To, RangeRules.OtherReportsMaxDays);
}
