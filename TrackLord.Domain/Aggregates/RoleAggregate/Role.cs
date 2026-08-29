using System;
using System.Collections.Generic;
using System.Text.Json;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Common;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Domain.Aggregates.RoleAggregate;

public class Role : AggregateRoot<Guid>, ITenantScopedEntity
{
    public Guid TenantId { get; private set; } = Tenant.DefaultTenantId;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsSystemRole { get; private set; }
    public bool IsActive { get; private set; }
    public string PermissionsJson { get; private set; } = "{}";
    public DateTime CreatedAtUtc { get; private set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private Role() { } // EF Core constructor

    public Role(
        Guid id,
        string name,
        string? description = null,
        bool isSystemRole = false,
        string? permissionsJson = null,
        Guid? tenantId = null,
        DateTime? createdAtUtc = null)
    {
        if (id == Guid.Empty) throw new DomainException("Role Id cannot be empty.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Role name is required.");

        Id = id;
        TenantId = tenantId.HasValue && tenantId.Value != Guid.Empty ? tenantId.Value : Tenant.DefaultTenantId;
        Name = name.Trim();
        Description = description?.Trim();
        IsSystemRole = isSystemRole;
        IsActive = true;
        PermissionsJson = string.IsNullOrWhiteSpace(permissionsJson) ? "{}" : permissionsJson.Trim();
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? description, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Role name is required.");

        // System role names cannot be modified if Admin
        if (IsSystemRole && Name.Equals("Admin", StringComparison.OrdinalIgnoreCase) && !name.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("The system Admin role cannot be renamed.");
        }

        Name = name.Trim();
        Description = description?.Trim();
        IsActive = isActive;
    }

    public void UpdatePermissions(string permissionsJson)
    {
        if (IsSystemRole && Name.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            // Admin role always maintains all permissions
            return;
        }

        PermissionsJson = string.IsNullOrWhiteSpace(permissionsJson) ? "{}" : permissionsJson.Trim();
    }

    public bool HasPermission(string feature, string accessLevel)
    {
        // System Admin role has unconditional bypass
        if (IsSystemRole && Name.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(PermissionsJson) || PermissionsJson == "{}")
        {
            return false;
        }

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(PermissionsJson, JsonOptions);
            if (dict == null) return false;

            foreach (var kvp in dict)
            {
                if (kvp.Key.Equals(feature, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value.Exists(a => a.Equals(accessLevel, StringComparison.OrdinalIgnoreCase));
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public Dictionary<string, List<string>> GetPermissionsDictionary()
    {
        if (IsSystemRole && Name.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            var adminDict = new Dictionary<string, List<string>>();
            foreach (var item in PermissionCatalog.All)
            {
                adminDict[item.Feature] = new List<string>(item.AvailableAccessLevels);
            }
            return adminDict;
        }

        if (string.IsNullOrWhiteSpace(PermissionsJson) || PermissionsJson == "{}")
        {
            return new Dictionary<string, List<string>>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(PermissionsJson, JsonOptions)
                   ?? new Dictionary<string, List<string>>();
        }
        catch
        {
            return new Dictionary<string, List<string>>();
        }
    }

    public void Deactivate()
    {
        if (IsSystemRole && Name.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("The system Admin role cannot be deactivated.");
        }
        IsActive = false;
    }

    public void Activate() => IsActive = true;

    public static List<Role> CreateDefaultSystemRoles(Guid tenantId)
    {
        var now = DateTime.UtcNow;

        // 1. Admin Role (All permissions)
        var adminPermissions = new Dictionary<string, List<string>>();
        foreach (var item in PermissionCatalog.All)
        {
            adminPermissions[item.Feature] = new List<string>(item.AvailableAccessLevels);
        }

        // 2. Supervisor Role (Floor oversight, movements, steps, rejections, materials)
        var supervisorPermissions = new Dictionary<string, List<string>>
        {
            [PermissionFeatures.ProductionLots] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.Movements] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.MovementReversals] = new() { AccessLevels.Write },
            [PermissionFeatures.MaterialLots] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.MaterialConsumptions] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.RejectionRecords] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.ManufacturingSteps] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.Clients] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.Users] = new() { AccessLevels.Read },
            [PermissionFeatures.Dashboard] = new() { AccessLevels.Read },
            [PermissionFeatures.Imports] = new() { AccessLevels.Write }
        };

        // 3. Manager Role (Full operations + users + config view)
        var managerPermissions = new Dictionary<string, List<string>>
        {
            [PermissionFeatures.ProductionLots] = new() { AccessLevels.Read, AccessLevels.Write, AccessLevels.Delete },
            [PermissionFeatures.Movements] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.MovementReversals] = new() { AccessLevels.Write },
            [PermissionFeatures.MaterialLots] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.MaterialConsumptions] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.RejectionRecords] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.ManufacturingSteps] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.Clients] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.Users] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.Roles] = new() { AccessLevels.Read },
            [PermissionFeatures.Dashboard] = new() { AccessLevels.Read },
            [PermissionFeatures.Imports] = new() { AccessLevels.Write }
        };

        // 4. Operator Role (Movement & consumption recording)
        var operatorPermissions = new Dictionary<string, List<string>>
        {
            [PermissionFeatures.ProductionLots] = new() { AccessLevels.Read },
            [PermissionFeatures.Movements] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.MaterialLots] = new() { AccessLevels.Read },
            [PermissionFeatures.MaterialConsumptions] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.RejectionRecords] = new() { AccessLevels.Read, AccessLevels.Write },
            [PermissionFeatures.Dashboard] = new() { AccessLevels.Read }
        };

        return new List<Role>
        {
            new(Guid.NewGuid(), "Admin", "Unrestricted administrator access across all features", isSystemRole: true, JsonSerializer.Serialize(adminPermissions), tenantId, now),
            new(Guid.NewGuid(), "Supervisor", "Production oversight, step configuration, and movement reversals", isSystemRole: true, JsonSerializer.Serialize(supervisorPermissions), tenantId, now),
            new(Guid.NewGuid(), "Manager", "Plant manager with team and operational oversight", isSystemRole: true, JsonSerializer.Serialize(managerPermissions), tenantId, now),
            new(Guid.NewGuid(), "Operator", "Factory floor worker for movements, material logs, and defect reporting", isSystemRole: true, JsonSerializer.Serialize(operatorPermissions), tenantId, now)
        };
    }
}
