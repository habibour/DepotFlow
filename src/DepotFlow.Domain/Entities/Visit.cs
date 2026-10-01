namespace DepotFlow.Domain.Entities;

public class Visit
{
    private Visit() { }   // for EF Core

    public Visit(
        Container container,
        ShippingLine shippingLine,
        DateTime gateInAtUtc,
        string truckInNumber,
        string sealNumber,
        string? damageNotesIn,
        string createdByUserId)
    {
        Container = container;
        ContainerId = container.Id;
        ShippingLine = shippingLine;
        ShippingLineId = shippingLine.Id;
        Status = VisitStatus.InYard;
        GateInAtUtc = gateInAtUtc;
        TruckInNumber = truckInNumber;
        SealNumber = sealNumber;
        DamageNotesIn = damageNotesIn;
        CreatedByUserId = createdByUserId;
    }

    public long Id { get; private set; }
    public int ContainerId { get; private set; }
    public Container Container { get; private set; } = null!;
    public int ShippingLineId { get; private set; }
    public ShippingLine ShippingLine { get; private set; } = null!;
    public VisitStatus Status { get; private set; }
    public DateTime GateInAtUtc { get; private set; }
    public DateTime? GateOutAtUtc { get; private set; }
    public string TruckInNumber { get; private set; } = null!;
    public string? TruckOutNumber { get; private set; }
    public string SealNumber { get; private set; } = null!;
    public string? DamageNotesIn { get; private set; }
    public string? DamageNotesOut { get; private set; }
    public int? YardSlotId { get; private set; }
    public YardSlot? YardSlot { get; private set; }
    public string CreatedByUserId { get; private set; } = null!;
    public string? ClosedByUserId { get; private set; }

    /// <summary>Puts the container in <paramref name="slot"/> (first placement or a relocation).</summary>
    public void AssignSlot(YardSlot slot)
    {
        if (Status != VisitStatus.InYard)
        {
            throw new DomainException("Only a visit that is in the yard can be placed in a slot.");
        }

        YardSlot = slot;
        YardSlotId = slot.Id;
    }

    public void GateOut(DateTime gateOutAtUtc, string truckOutNumber, string? damageNotesOut, string closedByUserId)
    {
        if (Status == VisitStatus.Released)
        {
            throw new DomainException("Visit has already been released.");
        }

        if (gateOutAtUtc < GateInAtUtc)
        {
            throw new DomainException("Gate-out cannot be earlier than gate-in.");
        }

        Status = VisitStatus.Released;
        YardSlot = null;      // the slot is free again
        YardSlotId = null;
        GateOutAtUtc = gateOutAtUtc;
        TruckOutNumber = truckOutNumber;
        DamageNotesOut = damageNotesOut;
        ClosedByUserId = closedByUserId;
    }
}
