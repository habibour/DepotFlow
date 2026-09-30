namespace DepotFlow.Domain.Entities;

public class ShippingLine
{
    private ShippingLine() { }   // for EF Core

    public ShippingLine(string code, string name, DateTime createdAtUtc)
    {
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public int Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public void Update(string name, bool isActive)
    {
        Name = name.Trim();
        IsActive = isActive;
    }
}
