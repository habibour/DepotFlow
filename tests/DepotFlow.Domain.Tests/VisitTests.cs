using DepotFlow.Domain;
using DepotFlow.Domain.Entities;

namespace DepotFlow.Domain.Tests;

// Covers spec S1-06 (Visit.GateOut behaviour).
public class VisitTests
{
    private static readonly DateTime GateIn = new(2026, 10, 1, 4, 0, 0, DateTimeKind.Utc);

    private static Visit NewVisit()
    {
        ContainerNumber.TryCreate("CSQU3054383", out var number, out _);
        var container = new Container(number!, 20, GateIn);
        var line = new ShippingLine("CMDU", "CMA CGM", GateIn);
        return new Visit(container, line, GateIn, "TRUCK-1", "SL1", null, "user-1");
    }

    [Fact]
    public void New_visit_starts_in_yard()
    {
        var visit = NewVisit();

        Assert.Equal(VisitStatus.InYard, visit.Status);
        Assert.Null(visit.GateOutAtUtc);
    }

    [Fact]
    public void Gate_out_releases_the_visit()
    {
        var visit = NewVisit();

        visit.GateOut(GateIn.AddHours(5), "TRUCK-2", "scratch", "user-2");

        Assert.Equal(VisitStatus.Released, visit.Status);
        Assert.Equal(GateIn.AddHours(5), visit.GateOutAtUtc);
        Assert.Equal("user-2", visit.ClosedByUserId);
    }

    [Fact]
    public void Cannot_release_twice()
    {
        var visit = NewVisit();
        visit.GateOut(GateIn.AddHours(5), "TRUCK-2", null, "user-2");

        Assert.Throws<DomainException>(() => visit.GateOut(GateIn.AddHours(6), "TRUCK-3", null, "user-2"));
    }

    [Fact]
    public void Container_size_must_be_20_or_40()
    {
        ContainerNumber.TryCreate("CSQU3054383", out var number, out _);

        Assert.Throws<DomainException>(() => new Container(number!, 30, GateIn));
    }
}
