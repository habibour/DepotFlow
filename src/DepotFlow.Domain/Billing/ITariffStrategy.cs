namespace DepotFlow.Domain.Billing;

/// <summary>One way of turning a number of dwell days into a charge. Implementations are interchangeable.</summary>
public interface ITariffStrategy
{
    ChargeResult Calculate(TariffSnapshot tariff, int dwellDays);
}
