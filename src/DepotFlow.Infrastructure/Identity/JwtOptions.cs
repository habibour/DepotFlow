namespace DepotFlow.Infrastructure.Identity;

/// <summary>Bound from the "Jwt" configuration section. The key is a secret and is never committed.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "DepotFlow";
    public string Audience { get; set; } = "DepotFlow.Api";
    public int ExpiryMinutes { get; set; } = 60;
}
