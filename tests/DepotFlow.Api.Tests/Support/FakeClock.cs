using DepotFlow.Application;

namespace DepotFlow.Api.Tests.Support;

/// <summary>
/// Real time plus an offset. Time still moves between calls (so timestamps stay in order), but a test can
/// jump forward days instead of waiting. Create every signed-in client BEFORE advancing: login tokens are
/// stamped with this clock, while the JWT middleware checks them against real time.
/// </summary>
public sealed class FakeClock : IClock
{
    private long _offsetTicks;

    public DateTime UtcNow => DateTime.UtcNow.AddTicks(Interlocked.Read(ref _offsetTicks));

    public void Advance(TimeSpan by) => Interlocked.Add(ref _offsetTicks, by.Ticks);

    /// <summary>
    /// Jumps to 12:00 Dhaka time, <paramref name="days"/> calendar days after the clock's current Dhaka date.
    /// Midday keeps day counts stable whatever time the test runs. Calling it with 0 moves to midday today.
    /// </summary>
    public void AdvanceToDhakaMidday(int days)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(UtcNow, zone);
        var target = new DateTime(localNow.Year, localNow.Month, localNow.Day, 12, 0, 0, DateTimeKind.Unspecified).AddDays(days);
        Interlocked.Exchange(ref _offsetTicks, (TimeZoneInfo.ConvertTimeToUtc(target, zone) - DateTime.UtcNow).Ticks);
    }

    public void Reset() => Interlocked.Exchange(ref _offsetTicks, 0);
}
