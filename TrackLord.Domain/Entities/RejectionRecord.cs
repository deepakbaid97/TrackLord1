using System;
using TrackLord.Domain.Common;
using TrackLord.Domain.Events;
using TrackLord.Domain.Exceptions;

namespace TrackLord.Domain.Entities;

public class RejectionRecord : BaseEntity<Guid>
{
    public Guid ProductionLotId { get; private set; }
    public Guid StepId { get; private set; }
    public decimal QuantityRejected { get; private set; }
    public string Reason { get; private set; } = default!;
    public DateTime RejectionDate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string? CreatedBy { get; private set; }

    private RejectionRecord() { } // EF Core

    public static RejectionRecord Create(
        Guid id,
        Guid productionLotId,
        Guid stepId,
        decimal quantityRejected,
        string reason,
        DateTime rejectionDate,
        string? createdBy = null)
    {
        if (id == Guid.Empty) throw new DomainException("Rejection Id cannot be empty.");
        if (productionLotId == Guid.Empty) throw new DomainException("ProductionLotId cannot be empty.");
        if (stepId == Guid.Empty) throw new DomainException("StepId cannot be empty.");
        if (quantityRejected <= 0)
        {
            throw new DomainException($"Rejected quantity must be positive. Provided: {quantityRejected}");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Rejection reason cannot be empty.");
        }

        return new RejectionRecord
        {
            Id = id,
            ProductionLotId = productionLotId,
            StepId = stepId,
            QuantityRejected = quantityRejected,
            Reason = reason.Trim(),
            RejectionDate = rejectionDate,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }
}
