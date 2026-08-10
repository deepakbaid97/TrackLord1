using System;
using TrackLord.Domain.Common;
using TrackLord.Domain.Exceptions;

namespace TrackLord.Domain.Aggregates.TenantAggregate;

public class Tenant : AggregateRoot<Guid>
{
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }
    public bool IsActive { get; private set; }

    private Tenant() { } // EF Core

    public Tenant(Guid id, string name, string code, DateTime? createdAtUtc = null)
    {
        if (id == Guid.Empty) throw new DomainException("Tenant Id cannot be empty.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Tenant name is required.");
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("Tenant code is required.");

        Id = id;
        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
        IsActive = true;
    }

    public void UpdateDetails(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Tenant name is required.");
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("Tenant code is required.");

        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
