using DepotFlow.Domain;

namespace DepotFlow.Domain.Tests;

// Covers spec S1-08 (dwell days).
public class DwellCalculatorTests
{
    private static readonly TimeZoneInfo Dhaka = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");

    // Builds a UTC instant from a Dhaka local time.
    private static DateTime Local(int month, int day, int hour, int minute) =>
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, month, day, hour, minute, 0, DateTimeKind.Unspecified), Dhaka);

    [Theory]
    [InlineData(10, 1, 9, 0, 10, 1, 17, 0, 1)]
    [InlineData(10, 1, 23, 50, 10, 2, 0, 10, 2)]
    [InlineData(10, 1, 9, 0, 10, 12, 9, 0, 12)]
    [InlineData(9, 30, 10, 0, 10, 1, 10, 0, 2)]
    public void Counts_calendar_days_in_depot_local_time(
        int inMonth, int inDay, int inHour, int inMinute,
        int outMonth, int outDay, int outHour, int outMinute,
        int expectedDays)
    {
        var days = DwellCalculator.Days(
            Local(inMonth, inDay, inHour, inMinute),
            Local(outMonth, outDay, outHour, outMinute),
            Dhaka);

        Assert.Equal(expectedDays, days);
    }

    [Fact]
    public void Gate_out_before_gate_in_is_a_domain_error()
    {
        Assert.Throws<DomainException>(() =>
            DwellCalculator.Days(Local(10, 2, 9, 0), Local(10, 1, 9, 0), Dhaka));
    }
}
