using System;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Common;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Events;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Domain.Aggregates.ProductionLotAggregate;

public class ProductionLot : AggregateRoot<Guid>, ITenantScopedEntity
{
    public Guid TenantId { get; private set; } = Tenant.DefaultTenantId;
    public ProductionLotNumber LotNumber { get; private set; } = default!;
    public Guid ItemId { get; private set; }
    public Quantity InitialQuantity { get; private set; } = default!;
    public Guid InitialStepId { get; private set; }
    public LotStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? ClientId { get; private set; }
    public string? ClientName { get; private set; }
    public string? CreatedBy { get; private set; }
    public string? Remarks { get; private set; }

    private ProductionLot() { } // EF Core

    public static ProductionLot Create(
        Guid id,
        ProductionLotNumber lotNumber,
        Guid itemId,
        Quantity initialQuantity,
        Guid initialStepId,
        string? createdBy = null,
        string? remarks = null,
        Guid? tenantId = null,
        Guid? clientId = null,
        string? clientName = null)
    {
        if (id == Guid.Empty) throw new DomainException("ProductionLot Id cannot be empty.");
        if (lotNumber is null) throw new DomainException("ProductionLotNumber cannot be null.");
        if (itemId == Guid.Empty) throw new DomainException("ItemId cannot be empty.");
        if (initialQuantity is null || initialQuantity.Value <= 0)
        {
            throw new DomainException("Initial production lot quantity must be greater than zero.");
        }
        if (initialStepId == Guid.Empty) throw new DomainException("Initial step Id cannot be empty.");

        var lot = new ProductionLot
        {
            Id = id,
            TenantId = tenantId.HasValue && tenantId.Value != Guid.Empty ? tenantId.Value : Tenant.DefaultTenantId,
            LotNumber = lotNumber,
            ItemId = itemId,
            InitialQuantity = initialQuantity,
            InitialStepId = initialStepId,
            Status = LotStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
            Remarks = remarks,
            ClientId = clientId,
            ClientName = clientName
        };

        lot.AddDomainEvent(new ProductionLotCreatedEvent(
            lot.Id,
            lot.LotNumber,
            lot.ItemId,
            lot.InitialQuantity.Value,
            lot.InitialQuantity.Unit,
            lot.InitialStepId,
            lot.CreatedAtUtc));

        return lot;
    }

    public void UpdateDetails(Guid? clientId, string? clientName, DateTime? createdAtUtc, string? remarks = null)
    {
        ClientId = clientId.HasValue && clientId.Value != Guid.Empty ? clientId.Value : null;
        ClientName = string.IsNullOrWhiteSpace(clientName) ? null : clientName.Trim();
        if (createdAtUtc.HasValue)
        {
            CreatedAtUtc = createdAtUtc.Value.Kind == DateTimeKind.Utc
                ? createdAtUtc.Value
                : DateTime.SpecifyKind(createdAtUtc.Value, DateTimeKind.Utc);
        }
        if (remarks != null)
        {
            Remarks = remarks.Trim();
        }
    }

    public void CompleteLot()
    {
        if (Status != LotStatus.Active)
        {
            throw new DomainException($"Cannot complete production lot in status {Status}.");
        }

        Status = LotStatus.Completed;
    }

    public void CancelLot(string reason)
    {
        if (Status != LotStatus.Active)
        {
            throw new DomainException($"Cannot cancel production lot in status {Status}.");
        }

        Status = LotStatus.Cancelled;
    }
}
