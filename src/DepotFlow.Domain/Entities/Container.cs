namespace DepotFlow.Domain.Entities;

public class Container
{
    private Container() { }   // for EF Core

    public Container(ContainerNumber number, int sizeFeet, DateTime createdAtUtc)
    {
        if (sizeFeet is not (20 or 40))
        {
            throw new DomainException("Container size must be 20 or 40 feet.");
        }

        Number = number.Value;
        SizeFeet = sizeFeet;
        CreatedAtUtc = createdAtUtc;
    }

    public int Id { get; private set; }
    public string Number { get; private set; } = null!;
    public int SizeFeet { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public List<Visit> Visits { get; private set; } = [];
}
