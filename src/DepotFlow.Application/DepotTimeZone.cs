namespace DepotFlow.Application;

public static class DepotTimeZone
{
    /// <summary>The depot's local time zone, used only to count dwell days.</summary>
    public static readonly TimeZoneInfo Dhaka = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");
}
