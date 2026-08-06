using System;
using TrackLord.Domain.Common;
using TrackLord.Domain.Enums;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Domain.Events;

public sealed record ProductionLotCreatedEvent(
    Guid LotId,
    ProductionLotNumber LotNumber,
    Guid ItemId,
    decimal InitialQuantity,
    UnitType Unit,
    Guid InitialStepId,
    DateTime CreatedAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record MaterialLotCreatedEvent(
    Guid MaterialLotId,
    MaterialLotNumber LotNumber,
    Guid MaterialItemId,
    decimal InitialQuantity,
    UnitType Unit,
    DateTime CreatedAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record LotMovedEvent(
    Guid MovementId,
    Guid ProductionLotId,
    Guid? FromStepId,
    Guid ToStepId,
    decimal Quantity,
    DateTime MovementDate,
    DateTime CreatedAtUtc,
    string? User,
    string? ReferenceNumber) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record MaterialConsumedEvent(
    Guid ConsumptionId,
    Guid ProductionLotId,
    Guid MaterialLotId,
    Guid StepId,
    decimal QuantityConsumed,
    UnitType Unit,
    DateTime ConsumptionDate,
    DateTime CreatedAtUtc,
    string? User) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record ItemsRejectedEvent(
    Guid RejectionId,
    Guid ProductionLotId,
    Guid StepId,
    decimal QuantityRejected,
    string Reason,
    DateTime RejectionDate,
    DateTime CreatedAtUtc,
    string? User) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record MovementReversedEvent(
    Guid OriginalMovementId,
    Guid ReversalMovementId,
    Guid ProductionLotId,
    string Reason,
    DateTime ReversedAtUtc,
    string? User) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record InventoryAdjustedEvent(
    Guid LotId,
    Guid StepId,
    decimal QuantityDelta,
    string Reason,
    DateTime AdjustmentDate,
    DateTime CreatedAtUtc,
    string? User) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
