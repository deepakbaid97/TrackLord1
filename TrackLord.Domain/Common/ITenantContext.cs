using System;

namespace TrackLord.Domain.Common;

/// <summary>
/// Service interface for resolving the active tenant context in the current execution.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    void SetTenantId(Guid tenantId);
}
