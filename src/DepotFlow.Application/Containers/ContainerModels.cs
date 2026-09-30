using DepotFlow.Domain.Entities;

namespace DepotFlow.Application.Containers;

public sealed record ContainerListItemDto(int Id, string Number, int SizeFeet);

public sealed record ContainerVisitDto(
    long Id,
    VisitStatus Status,
    string ShippingLineCode,
    DateTime GateInAtUtc,
    DateTime? GateOutAtUtc);

public sealed record ContainerDetailDto(int Id, string Number, int SizeFeet, IReadOnlyList<ContainerVisitDto> Visits);
