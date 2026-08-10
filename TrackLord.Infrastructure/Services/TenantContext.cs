using System;
using Microsoft.AspNetCore.Http;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Common;

namespace TrackLord.Infrastructure.Services;

public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _overriddenTenantId;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            if (_overriddenTenantId.HasValue && _overriddenTenantId.Value != Guid.Empty)
            {
                return _overriddenTenantId.Value;
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                // 1. Check X-Tenant-Id header
                if (httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var headerVal) &&
                    Guid.TryParse(headerVal.ToString(), out var headerTenantId) &&
                    headerTenantId != Guid.Empty)
                {
                    return headerTenantId;
                }

                // 2. Check tenant_id JWT claim
                var tenantClaim = httpContext.User.FindFirst("tenant_id")?.Value
                                  ?? httpContext.User.FindFirst("TenantId")?.Value;
                if (!string.IsNullOrWhiteSpace(tenantClaim) && Guid.TryParse(tenantClaim, out var claimTenantId) && claimTenantId != Guid.Empty)
                {
                    return claimTenantId;
                }
            }

            return Tenant.DefaultTenantId;
        }
    }

    public void SetTenantId(Guid tenantId)
    {
        if (tenantId != Guid.Empty)
        {
            _overriddenTenantId = tenantId;
        }
    }
}
