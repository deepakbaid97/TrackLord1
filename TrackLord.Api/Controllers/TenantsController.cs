using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackLord.Api.Authorization;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Common;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantContext _tenantContext;

    public TenantsController(ITenantRepository tenantRepository, ITenantContext tenantContext)
    {
        _tenantRepository = tenantRepository;
        _tenantContext = tenantContext;
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
            t.IsActive,
            t.SettingsJson
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
            tenant.IsActive,
            tenant.SettingsJson
        });
    }

    /// <summary>
    /// Gets settings for the current active tenant.
    /// </summary>
    [HttpGet("current/settings")]
    [Authorize]
    [RequirePermission(PermissionFeatures.TenantSettings, AccessLevels.Read)]
    public async Task<IActionResult> GetCurrentTenantSettings()
    {
        var tenantId = _tenantContext.TenantId;
        var tenant = await _tenantRepository.GetByIdAsync(tenantId);
        if (tenant == null)
        {
            return NotFound(new { message = $"Tenant with ID {tenantId} not found." });
        }

        var settings = ParseSettings(tenant.SettingsJson);
        return Ok(new
        {
            tenantId = tenant.Id,
            tenantName = tenant.Name,
            clientMode = FormatClientMode(settings.ClientMode),
            featureOverrides = settings.FeatureOverrides ?? new Dictionary<string, bool>(),
            settingsJson = tenant.SettingsJson
        });
    }

    /// <summary>
    /// Updates settings for the current active tenant.
    /// </summary>
    [HttpPut("current/settings")]
    [Authorize]
    [RequirePermission(PermissionFeatures.TenantSettings, AccessLevels.Write)]
    public async Task<IActionResult> UpdateCurrentTenantSettings([FromBody] UpdateTenantSettingsRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        var tenant = await _tenantRepository.GetByIdAsync(tenantId);
        if (tenant == null)
        {
            return NotFound(new { message = $"Tenant with ID {tenantId} not found." });
        }

        var clientMode = ClientTrackingMode.StepLevel;
        if (!string.IsNullOrWhiteSpace(request.ClientMode))
        {
            if (Enum.TryParse<ClientTrackingMode>(request.ClientMode, true, out var parsedMode))
            {
                clientMode = parsedMode;
            }
            else if (request.ClientMode.Equals("lot", StringComparison.OrdinalIgnoreCase))
            {
                clientMode = ClientTrackingMode.LotLevel;
            }
            else if (request.ClientMode.Equals("step", StringComparison.OrdinalIgnoreCase))
            {
                clientMode = ClientTrackingMode.StepLevel;
            }
            else if (request.ClientMode.Equals("none", StringComparison.OrdinalIgnoreCase) || request.ClientMode.Equals("disabled", StringComparison.OrdinalIgnoreCase))
            {
                clientMode = ClientTrackingMode.None;
            }
        }

        var newSettings = new TenantSettings
        {
            ClientMode = clientMode,
            FeatureOverrides = request.FeatureOverrides ?? new Dictionary<string, bool>()
        };

        var json = JsonSerializer.Serialize(newSettings, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false });
        tenant.UpdateSettings(json);
        await _tenantRepository.UpdateAsync(tenant);

        return Ok(new
        {
            tenantId = tenant.Id,
            tenantName = tenant.Name,
            clientMode = FormatClientMode(newSettings.ClientMode),
            featureOverrides = newSettings.FeatureOverrides,
            settingsJson = json,
            message = "Tenant configuration updated successfully."
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
            tenant.IsActive,
            tenant.SettingsJson
        });
    }

    private static TenantSettings ParseSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new TenantSettings();
        try
        {
            return JsonSerializer.Deserialize<TenantSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TenantSettings();
        }
        catch
        {
            return new TenantSettings();
        }
    }

    private static string FormatClientMode(ClientTrackingMode mode) => mode switch
    {
        ClientTrackingMode.LotLevel => "lot",
        ClientTrackingMode.StepLevel => "step",
        ClientTrackingMode.None => "none",
        _ => "step"
    };
}

public record CreateTenantRequest(string Name, string Code);

public record UpdateTenantSettingsRequest(
    string? ClientMode,
    Dictionary<string, bool>? FeatureOverrides);
