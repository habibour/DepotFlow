namespace DepotFlow.Domain;

public static class DwellCalculator
{
    /// <summary>
    /// Number of calendar days a container occupied the depot, counted in the depot's
    /// local time, with both the gate-in day and the gate-out day counted.
    /// Gate-in 1 Oct 23:50 and gate-out 2 Oct 00:10 is 2 days.
    /// </summary>
    public static int Days(DateTime gateInUtc, DateTime gateOutUtc, TimeZoneInfo depotZone)
    {
        if (gateOutUtc < gateInUtc)
        {
            throw new DomainException("Gate-out cannot be earlier than gate-in.");
        }

        var inDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(gateInUtc, depotZone));
        var outDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(gateOutUtc, depotZone));

        return outDate.DayNumber - inDate.DayNumber + 1;
    }
}
