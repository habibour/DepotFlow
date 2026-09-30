namespace DepotFlow.Application;

/// <summary>Source of the current UTC time, so business code never calls DateTime.UtcNow and tests can fake time.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
