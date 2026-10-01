using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DepotFlow.Domain;

namespace DepotFlow.Api.Tests.Support;

public static class TestData
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static int _serial = 1000;

    /// <summary>A valid, never-before-used container number (correct ISO 6346 check digit).</summary>
    public static string NewContainerNumber()
    {
        var first10 = $"TSTU{Interlocked.Increment(ref _serial):D6}";
        return first10 + ContainerNumber.ComputeCheckDigit(first10);
    }

    public static object GateInBody(string containerNumber, int sizeFeet = 20) => new
    {
        containerNumber,
        sizeFeet,
        shippingLineId = 1,
        truckNumber = "DHK-TA-11-2345",
        sealNumber = "SL889201",
        damageNotes = (string?)null
    };

    /// <summary>Free 4 days; days 5-10 at 200; day 11 onwards at 400.</summary>
    public static object TieredTariffBody(int sizeFeet = 20) => new
    {
        sizeFeet,
        strategyKey = "Tiered",
        freeDays = 4,
        tiers = new object[]
        {
            new { fromDay = 5, toDay = (int?)10, ratePerDay = 200.00m },
            new { fromDay = 11, toDay = (int?)null, ratePerDay = 400.00m }
        }
    };

    public static object GateOutBody() => new { truckNumber = "DHK-TA-11-9999", damageNotes = (string?)null };

    /// <summary>The machine-readable "code" field of a problem response.</summary>
    public static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;
}
