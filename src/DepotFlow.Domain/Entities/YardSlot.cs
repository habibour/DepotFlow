using DepotFlow.Domain.Yard;

namespace DepotFlow.Domain.Entities;

public class YardSlot
{
    private YardSlot() { }   // for EF Core

    public YardSlot(string block, int row, int bay, int tier)
    {
        Block = block;
        Row = row;
        Bay = bay;
        Tier = tier;
    }

    public int Id { get; private set; }
    public string Block { get; private set; } = null!;
    public int Row { get; private set; }
    public int Bay { get; private set; }
    public int Tier { get; private set; }

    /// <summary>Computed by the database, for example A-01-03-2. Read-only here.</summary>
    public string Code { get; private set; } = null!;

    public SlotPosition Position => new(Block, Row, Bay, Tier);
}
