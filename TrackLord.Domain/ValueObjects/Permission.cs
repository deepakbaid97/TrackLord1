using System;
using System.Collections.Generic;

namespace TrackLord.Domain.ValueObjects;

public static class PermissionFeatures
{
    public const string ProductionLots = "production_lots";
    public const string Movements = "movements";
    public const string MovementReversals = "movement_reversals";
    public const string MaterialLots = "material_lots";
    public const string MaterialConsumptions = "material_consumptions";
    public const string RejectionRecords = "rejection_records";
    public const string ManufacturingSteps = "manufacturing_steps";
    public const string Clients = "clients";
    public const string Users = "users";
    public const string Roles = "roles";
    public const string TenantSettings = "tenant_settings";
    public const string Dashboard = "dashboard";
    public const string Imports = "imports";
}

public static class AccessLevels
{
    public const string Read = "read";
    public const string Write = "write";
    public const string Delete = "delete";
}

public record PermissionDefinition(string Feature, string Label, string Description, IReadOnlyList<string> AvailableAccessLevels);

public static class PermissionCatalog
{
    public static readonly IReadOnlyList<PermissionDefinition> All = new List<PermissionDefinition>
    {
        new(
            PermissionFeatures.ProductionLots,
            "Production Lots",
            "View, create, edit, or delete manufacturing production lots.",
            new[] { AccessLevels.Read, AccessLevels.Write, AccessLevels.Delete }
        ),
        new(
            PermissionFeatures.Movements,
            "Step Movements",
            "Perform quantity transfers between manufacturing steps.",
            new[] { AccessLevels.Read, AccessLevels.Write }
        ),
        new(
            PermissionFeatures.MovementReversals,
            "Movement Reversals",
            "Reverse historical step movements in inventory/WIP.",
            new[] { AccessLevels.Write }
        ),
        new(
            PermissionFeatures.MaterialLots,
            "Raw Material Lots",
            "Manage raw materials, lot creation, balance edits, and deletion.",
            new[] { AccessLevels.Read, AccessLevels.Write, AccessLevels.Delete }
        ),
        new(
            PermissionFeatures.MaterialConsumptions,
            "Material Consumptions",
            "Record and edit raw material consumption for production lots.",
            new[] { AccessLevels.Read, AccessLevels.Write }
        ),
        new(
            PermissionFeatures.RejectionRecords,
            "Defects & Rejections",
            "Log and view defective items, wastage, and rework targets.",
            new[] { AccessLevels.Read, AccessLevels.Write }
        ),
        new(
            PermissionFeatures.ManufacturingSteps,
            "Manufacturing Steps",
            "Configure, order, and activate/deactivate factory workflow steps.",
            new[] { AccessLevels.Read, AccessLevels.Write }
        ),
        new(
            PermissionFeatures.Clients,
            "Clients & Job Workers",
            "Manage clients, external contractors, and customer entities.",
            new[] { AccessLevels.Read, AccessLevels.Write }
        ),
        new(
            PermissionFeatures.Users,
            "Team Members",
            "View user profiles, invite new team members, and manage accounts.",
            new[] { AccessLevels.Read, AccessLevels.Write }
        ),
        new(
            PermissionFeatures.Roles,
            "Roles & Permissions",
            "Create, configure, and assign role-based permissions.",
            new[] { AccessLevels.Read, AccessLevels.Write }
        ),
        new(
            PermissionFeatures.TenantSettings,
            "Tenant Settings",
            "Configure organization profile and system tracking modes.",
            new[] { AccessLevels.Read, AccessLevels.Write }
        ),
        new(
            PermissionFeatures.Dashboard,
            "Analytics & Dashboard",
            "View factory overview, KPI cards, and step WIP breakdowns.",
            new[] { AccessLevels.Read }
        ),
        new(
            PermissionFeatures.Imports,
            "Bulk Data Imports",
            "Bulk import production lots and material batches via CSV/Excel.",
            new[] { AccessLevels.Write }
        )
    };
}
