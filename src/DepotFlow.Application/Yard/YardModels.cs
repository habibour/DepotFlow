namespace DepotFlow.Application.Yard;

public sealed record SlotOccupantDto(long VisitId, string ContainerNumber, int SizeFeet);

public sealed record YardSlotDto(int Id, string Code, string Block, int Row, int Bay, int Tier, SlotOccupantDto? Occupant);

public sealed record BlockOccupancyDto(string Block, int TotalSlots, int OccupiedSlots, decimal PercentOccupied);
