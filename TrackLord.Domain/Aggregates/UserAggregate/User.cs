using System;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Common;
using TrackLord.Domain.Exceptions;

namespace TrackLord.Domain.Aggregates.UserAggregate;

public class User : AggregateRoot<Guid>, ITenantScopedEntity
{
    public Guid TenantId { get; private set; } = Tenant.DefaultTenantId;
    public string PhoneNumber { get; private set; } = default!;
    public string? Name { get; private set; }
    public string Role { get; private set; } = "Operator";
    public string? FirebaseUid { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }
    public bool IsActive { get; private set; }

    private User() { } // EF Core constructor

    public User(Guid id, string phoneNumber, string? name = null, string role = "Operator", string? firebaseUid = null, DateTime? createdAtUtc = null, Guid? tenantId = null)
    {
        if (id == Guid.Empty) throw new DomainException("User Id cannot be empty.");
        if (string.IsNullOrWhiteSpace(phoneNumber)) throw new DomainException("User phone number is required.");

        Id = id;
        TenantId = tenantId.HasValue && tenantId.Value != Guid.Empty ? tenantId.Value : Tenant.DefaultTenantId;
        PhoneNumber = phoneNumber.Trim();
        Name = name?.Trim();
        Role = string.IsNullOrWhiteSpace(role) ? "Operator" : role.Trim();
        FirebaseUid = firebaseUid?.Trim();
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
        LastLoginAtUtc = DateTime.UtcNow;
        IsActive = true;
    }

    public void UpdateProfile(string? name, string role)
    {
        Name = name?.Trim();
        if (!string.IsNullOrWhiteSpace(role))
        {
            Role = role.Trim();
        }
    }

    public void SetFirebaseUid(string firebaseUid)
    {
        if (!string.IsNullOrWhiteSpace(firebaseUid))
        {
            FirebaseUid = firebaseUid.Trim();
        }
    }

    public void RecordLogin()
    {
        LastLoginAtUtc = DateTime.UtcNow;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
