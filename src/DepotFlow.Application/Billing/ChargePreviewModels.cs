namespace DepotFlow.Application.Billing;

public sealed record ChargeLineDto(int FromDay, int ToDay, int Days, decimal RatePerDay, decimal Amount);

public sealed record ChargePreviewDto(
    long VisitId,
    int DwellDays,
    int FreeDays,
    string StrategyKey,
    string Currency,
    IReadOnlyList<ChargeLineDto> Lines,
    decimal Total);
