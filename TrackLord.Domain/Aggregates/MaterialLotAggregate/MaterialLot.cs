using System;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Common;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Events;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Domain.Aggregates.MaterialLotAggregate;

public class MaterialLot : AggregateRoot<Guid>, ITenantScopedEntity
{
    public Guid TenantId { get; private set; } = Tenant.DefaultTenantId;
    public MaterialLotNumber LotNumber { get; private set; } = default!;
    public Guid MaterialItemId { get; private set; }
    public string? MaterialName { get; private set; }
    public Quantity InitialQuantity { get; private set; } = default!;
    public Quantity CurrentQuantity { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }

    private MaterialLot() { } // EF Core

    public static MaterialLot Create(
        Guid id,
        MaterialLotNumber lotNumber,
        Guid materialItemId,
        Quantity initialQuantity,
        string? materialName = null,
        Guid? tenantId = null)
    {
        if (id == Guid.Empty) throw new DomainException("MaterialLot Id cannot be empty.");
        if (lotNumber is null) throw new DomainException("MaterialLotNumber cannot be null.");
        if (materialItemId == Guid.Empty) throw new DomainException("MaterialItemId cannot be empty.");
        if (initialQuantity is null || initialQuantity.Value <= 0)
        {
            throw new DomainException("Initial material lot quantity must be greater than zero.");
        }

        var lot = new MaterialLot
        {
            Id = id,
            TenantId = tenantId.HasValue && tenantId.Value != Guid.Empty ? tenantId.Value : Tenant.DefaultTenantId,
            LotNumber = lotNumber,
            MaterialItemId = materialItemId,
            MaterialName = materialName,
            InitialQuantity = new Quantity(initialQuantity.Value, initialQuantity.Unit),
            CurrentQuantity = new Quantity(initialQuantity.Value, initialQuantity.Unit),
            CreatedAtUtc = DateTime.UtcNow
        };

        lot.AddDomainEvent(new MaterialLotCreatedEvent(
            lot.Id,
            lot.LotNumber,
            lot.MaterialItemId,
            lot.InitialQuantity.Value,
            lot.InitialQuantity.Unit,
            lot.CreatedAtUtc));

        return lot;
    }

    public void Consume(decimal quantityToConsume)
    {
        if (quantityToConsume < 0)
        {
            throw new DomainException("Material consumption quantity cannot be negative.");
        }

        if (CurrentQuantity.Value < quantityToConsume)
        {
            throw new DomainException(
                $"Insufficient material balance for lot '{LotNumber}'. Current balance: {CurrentQuantity.Value} {CurrentQuantity.Unit}, Requested: {quantityToConsume}");
        }

        CurrentQuantity = new Quantity(CurrentQuantity.Value - quantityToConsume, CurrentQuantity.Unit);
    }

    public void Restore(decimal quantityToRestore)
    {
        if (quantityToRestore < 0)
        {
            throw new DomainException("Restored material quantity cannot be negative.");
        }

        CurrentQuantity = new Quantity(CurrentQuantity.Value + quantityToRestore, CurrentQuantity.Unit);
    }
}
