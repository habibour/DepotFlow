namespace DepotFlow.Domain.Entities;

public class TariffTier
{
    private TariffTier() { }   // for EF Core

    public TariffTier(int fromDay, int? toDay, decimal ratePerDay)
    {
        FromDay = fromDay;
        ToDay = toDay;
        RatePerDay = ratePerDay;
    }

    public int Id { get; private set; }
    public int TariffId { get; private set; }
    public int FromDay { get; private set; }
    public int? ToDay { get; private set; }
    public decimal RatePerDay { get; private set; }
}
