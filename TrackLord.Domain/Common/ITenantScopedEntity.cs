using System;

namespace TrackLord.Domain.Common;

/// <summary>
/// Defines an entity that belongs to a specific tenant in a multi-tenant system.
/// </summary>
public interface ITenantScopedEntity
{
    Guid TenantId { get; }
}
