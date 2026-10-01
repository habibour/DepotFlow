namespace DepotFlow.Application.Reports;

// Rows exactly as the stored procedures return them (column names match the property names).
public sealed record DailyMovementRow(DateOnly Date, int GateIns, int GateOuts);

public sealed record YardOccupancyRow(string Block, int TotalSlots, int OccupiedSlots, int OccupiedTeu, decimal PercentOccupied);

public sealed record DwellTimeRow(
    int ShippingLineId, string Code, int ReleasedVisits, decimal AvgDwellDays, decimal MedianDwellDays, int MaxDwellDays);

public sealed record RevenueRow(
    int? ShippingLineId, string Code, int Invoices, decimal TotalBilled, decimal TotalPaid, decimal Outstanding);

// Requests: dates are local (Asia/Dhaka) dates, inclusive at both ends.
public sealed record DailyMovementsRequest(DateOnly? From, DateOnly? To, int? ShippingLineId);

public sealed record DwellTimeRequest(DateOnly? From, DateOnly? To, int? ShippingLineId);

public sealed record RevenueRequest(DateOnly? From, DateOnly? To);

// Responses
public sealed record GateMovementTotal(int GateIns, int GateOuts);

public sealed record DailyMovementsReport(
    DateOnly From, DateOnly To, int? ShippingLineId, IReadOnlyList<DailyMovementRow> Rows, GateMovementTotal Total);

public sealed record YardOccupancyReport(IReadOnlyList<YardOccupancyRow> Rows, YardOccupancyRow Total);

public sealed record DwellTimeReport(DateOnly From, DateOnly To, int? ShippingLineId, IReadOnlyList<DwellTimeRow> Rows);

public sealed record RevenueTotal(int Invoices, decimal TotalBilled, decimal TotalPaid, decimal Outstanding);

public sealed record RevenueReport(
    DateOnly From, DateOnly To, string Currency, IReadOnlyList<RevenueRow> Rows, RevenueTotal Total);
