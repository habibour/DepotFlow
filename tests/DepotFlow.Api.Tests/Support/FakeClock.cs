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

    public void Reset() => Interlocked.Exchange(ref _offsetTicks, 0);
}
