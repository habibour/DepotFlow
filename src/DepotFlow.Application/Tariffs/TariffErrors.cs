using DepotFlow.Application.Common;

namespace DepotFlow.Application.Tariffs;

public static class TariffErrors
{
    public static Error Invalid(string reason) => Error.Unprocessable("invalid_tariff", reason);

    public static readonly Error NotFound = Error.NotFound("tariff_not_found", "Tariff was not found.");

    public static readonly Error NoActiveTariff = Error.Unprocessable(
        "no_active_tariff", "There is no active tariff for this shipping line and container size.");

    public static readonly Error Conflict = Error.Conflict(
        "tariff_conflict", "Another change to this tariff was made at the same time. Please try again.");
}
