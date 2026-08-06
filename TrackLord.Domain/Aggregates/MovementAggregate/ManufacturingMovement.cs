using System;
using TrackLord.Domain.Common;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Events;
using TrackLord.Domain.Exceptions;

namespace TrackLord.Domain.Aggregates.MovementAggregate;

public class ManufacturingMovement : AggregateRoot<Guid>
{
    public Guid ProductionLotId { get; private set; }
    public Guid? FromStepId { get; private set; } // Null for initial production creation movement
    public Guid ToStepId { get; private set; }
    public decimal Quantity { get; private set; }
    public MovementType Type { get; private set; }
    public DateTime MovementDate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? ClientId { get; private set; }
    public string? ClientName { get; private set; }
    public string? CreatedBy { get; private set; }
    public string? Remarks { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public bool IsReversed { get; private set; }
    public Guid? ReversalMovementId { get; private set; }
    public string? ReversalReason { get; private set; }

    private ManufacturingMovement() { } // EF Core

    public static ManufacturingMovement Create(
        Guid id,
        Guid productionLotId,
        Guid? fromStepId,
        Guid toStepId,
        decimal quantity,
        MovementType type,
        DateTime movementDate,
        Guid? clientId = null,
        string? clientName = null,
        string? createdBy = null,
        string? remarks = null,
        string? referenceNumber = null)
    {
        if (id == Guid.Empty) throw new DomainException("Movement Id cannot be empty.");
        if (productionLotId == Guid.Empty) throw new DomainException("ProductionLotId cannot be empty.");
        if (toStepId == Guid.Empty) throw new DomainException("ToStepId cannot be empty.");
        if (quantity <= 0) throw new DomainException($"Movement quantity must be positive. Provided: {quantity}");
        if (type == MovementType.StepTransfer && fromStepId.HasValue && fromStepId.Value == toStepId)
        {
            throw new DomainException("Source step (FromStepId) and target step (ToStepId) cannot be the same.");
        }

        var utcNow = DateTime.UtcNow;
        var movement = new ManufacturingMovement
        {
            Id = id,
            ProductionLotId = productionLotId,
            FromStepId = fromStepId,
            ToStepId = toStepId,
            Quantity = quantity,
            Type = type,
            MovementDate = movementDate,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
            ClientId = clientId,
            ClientName = clientName,
            CreatedBy = createdBy,
            Remarks = remarks,
            ReferenceNumber = referenceNumber,
            IsReversed = false
        };

        movement.AddDomainEvent(new LotMovedEvent(
            movement.Id,
            movement.ProductionLotId,
            movement.FromStepId,
            movement.ToStepId,
            movement.Quantity,
            movement.MovementDate,
            movement.CreatedAtUtc,
            movement.CreatedBy,
            movement.ReferenceNumber));

        return movement;
    }

    public void EditMovement(
        Guid? fromStepId,
        Guid toStepId,
        decimal newQuantity,
        DateTime newMovementDate,
        string? remarks,
        string? referenceNumber)
    {
        if (IsReversed)
        {
            throw new DomainException("Cannot edit a reversed movement.");
        }

        if (toStepId == Guid.Empty) throw new DomainException("ToStepId cannot be empty.");
        if (newQuantity <= 0) throw new DomainException($"Movement quantity must be positive. Provided: {newQuantity}");
        if (Type == MovementType.StepTransfer && fromStepId.HasValue && fromStepId.Value == toStepId)
        {
            throw new DomainException("Source step (FromStepId) and target step (ToStepId) cannot be the same.");
        }

        FromStepId = fromStepId;
        ToStepId = toStepId;
        Quantity = newQuantity;
        MovementDate = newMovementDate;
        Remarks = remarks;
        ReferenceNumber = referenceNumber;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public ManufacturingMovement CreateReversal(Guid reversalMovementId, string reason, string? user)
    {
        if (IsReversed)
        {
            throw new DomainException("Movement is already reversed.");
        }

        if (reversalMovementId == Guid.Empty) throw new DomainException("Reversal Movement Id cannot be empty.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Reversal reason is required.");

        IsReversed = true;
        ReversalMovementId = reversalMovementId;
        ReversalReason = reason;
        UpdatedAtUtc = DateTime.UtcNow;

        // Reversal movement swaps FromStepId and ToStepId to return inventory
        var reversal = new ManufacturingMovement
        {
            Id = reversalMovementId,
            ProductionLotId = ProductionLotId,
            FromStepId = ToStepId,
            ToStepId = FromStepId ?? ToStepId,
            Quantity = Quantity,
            Type = MovementType.Reversal,
            MovementDate = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CreatedBy = user,
            Remarks = $"Reversal of Movement {Id}: {reason}",
            ReferenceNumber = $"REV-{ReferenceNumber ?? Id.ToString("N")[..8]}",
            IsReversed = false
        };

        AddDomainEvent(new MovementReversedEvent(
            Id,
            reversalMovementId,
            ProductionLotId,
            reason,
            DateTime.UtcNow,
            user));

        return reversal;
    }
}
