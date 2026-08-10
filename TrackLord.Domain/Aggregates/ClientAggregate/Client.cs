using System;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Common;
using TrackLord.Domain.Exceptions;

namespace TrackLord.Domain.Aggregates.ClientAggregate;

public class Client : AggregateRoot<Guid>, ITenantScopedEntity
{
    public Guid TenantId { get; private set; } = Tenant.DefaultTenantId;
    public string Name { get; private set; } = default!;
    public string PhoneNumber { get; private set; } = default!;
    public string ClientType { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }
    public bool IsActive { get; private set; }

    private Client() { } // EF Core

    public Client(Guid id, string name, string phoneNumber, string clientType, DateTime? createdAtUtc = null, Guid? tenantId = null)
    {
        if (id == Guid.Empty) throw new DomainException("Client Id cannot be empty.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Client name is required.");

        Id = id;
        TenantId = tenantId.HasValue && tenantId.Value != Guid.Empty ? tenantId.Value : Tenant.DefaultTenantId;
        Name = name.Trim();
        PhoneNumber = phoneNumber?.Trim() ?? string.Empty;
        ClientType = string.IsNullOrWhiteSpace(clientType) ? "General" : clientType.Trim();
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
        IsActive = true;
    }

    public void UpdateDetails(string name, string phoneNumber, string clientType)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Client name is required.");

        Name = name.Trim();
        PhoneNumber = phoneNumber?.Trim() ?? string.Empty;
        ClientType = string.IsNullOrWhiteSpace(clientType) ? "General" : clientType.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
