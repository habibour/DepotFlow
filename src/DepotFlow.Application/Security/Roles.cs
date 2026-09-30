namespace DepotFlow.Application.Security;

public static class Roles
{
    public const string Admin = "Admin";
    public const string GateClerk = "GateClerk";
    public const string YardPlanner = "YardPlanner";
    public const string BillingOfficer = "BillingOfficer";

    public static readonly string[] All = [Admin, GateClerk, YardPlanner, BillingOfficer];
}
