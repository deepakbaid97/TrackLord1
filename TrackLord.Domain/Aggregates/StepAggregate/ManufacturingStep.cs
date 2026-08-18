using System;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Common;
using TrackLord.Domain.Exceptions;

namespace TrackLord.Domain.Aggregates.StepAggregate;

public class ManufacturingStep : AggregateRoot<Guid>, ITenantScopedEntity
{
    public Guid TenantId { get; private set; } = Tenant.DefaultTenantId;
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public int SequenceOrder { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsTerminating { get; private set; }
    public Guid FactoryId { get; private set; }

    private ManufacturingStep() { } // EF Core

    public ManufacturingStep(Guid id, string code, string name, int sequenceOrder, Guid factoryId, bool isTerminating = false, Guid? tenantId = null)
    {
        if (id == Guid.Empty) throw new DomainException("Step Id cannot be empty.");
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("Step code cannot be empty.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Step name cannot be empty.");
        if (sequenceOrder < 0) throw new DomainException("Sequence order must be non-negative.");

        Id = id;
        TenantId = tenantId.HasValue && tenantId.Value != Guid.Empty ? tenantId.Value : Tenant.DefaultTenantId;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        SequenceOrder = sequenceOrder;
        FactoryId = factoryId;
        IsActive = true;
        IsTerminating = isTerminating;
    }

    public void UpdateDetails(string name, int sequenceOrder, bool isTerminating)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Step name cannot be empty.");
        if (sequenceOrder < 0) throw new DomainException("Sequence order must be non-negative.");

        Name = name.Trim();
        SequenceOrder = sequenceOrder;
        IsTerminating = isTerminating;
    }

    public void UpdateSequenceOrder(int sequenceOrder)
    {
        if (sequenceOrder < 0) throw new DomainException("Sequence order must be non-negative.");
        SequenceOrder = sequenceOrder;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
