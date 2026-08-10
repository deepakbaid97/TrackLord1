using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Repositories;

namespace TrackLord.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    private readonly ITenantRepository _tenantRepository;

    public TenantsController(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    /// <summary>
    /// Gets all active tenants in the system.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllTenants()
    {
        var tenants = await _tenantRepository.GetAllAsync();
        return Ok(tenants.Select(t => new
        {
            t.Id,
            t.Name,
            t.Code,
            t.CreatedAtUtc,
            t.IsActive
        }));
    }

    /// <summary>
    /// Gets tenant by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTenantById(Guid id)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id);
        if (tenant == null)
        {
            return NotFound(new { message = $"Tenant with ID {id} not found." });
        }

        return Ok(new
        {
            tenant.Id,
            tenant.Name,
            tenant.Code,
            tenant.CreatedAtUtc,
            tenant.IsActive
        });
    }

    /// <summary>
    /// Creates a new tenant organization.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { message = "Tenant Name and Code are required." });
        }

        var existing = await _tenantRepository.GetByCodeAsync(request.Code);
        if (existing != null)
        {
            return Conflict(new { message = $"Tenant with code '{request.Code}' already exists." });
        }

        var tenant = new Tenant(Guid.NewGuid(), request.Name, request.Code);
        await _tenantRepository.AddAsync(tenant);

        return CreatedAtAction(nameof(GetTenantById), new { id = tenant.Id }, new
        {
            tenant.Id,
            tenant.Name,
            tenant.Code,
            tenant.CreatedAtUtc,
            tenant.IsActive
        });
    }
}

public record CreateTenantRequest(string Name, string Code);
