using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackLord.Api.Authorization;
using TrackLord.Application.Commands;
using TrackLord.Application.Queries;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly IMediator _mediator;

    public RolesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets all roles available in the current tenant.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionFeatures.Roles, AccessLevels.Read)]
    public async Task<IActionResult> GetAllRoles()
    {
        var roles = await _mediator.Send(new GetAllRolesQuery());
        return Ok(roles);
    }

    /// <summary>
    /// Gets detailed information and permissions for a specific role.
    /// </summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionFeatures.Roles, AccessLevels.Read)]
    public async Task<IActionResult> GetRoleById(Guid id)
    {
        var role = await _mediator.Send(new GetRoleByIdQuery(id));
        if (role == null)
        {
            return NotFound(new { message = $"Role with ID '{id}' was not found." });
        }
        return Ok(role);
    }

    /// <summary>
    /// Returns the system catalog of all configurable permission features and access levels.
    /// </summary>
    [HttpGet("permissions/catalog")]
    [RequirePermission(PermissionFeatures.Roles, AccessLevels.Read)]
    public async Task<IActionResult> GetPermissionCatalog()
    {
        var catalog = await _mediator.Send(new GetPermissionCatalogQuery());
        return Ok(catalog);
    }

    /// <summary>
    /// Creates a new custom role with assigned permissions.
    /// </summary>
    [HttpPost]
    [RequirePermission(PermissionFeatures.Roles, AccessLevels.Write)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        var command = new CreateRoleCommand(request.Name, request.Description, request.PermissionsJson);
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetRoleById), new { id }, new { id });
    }

    /// <summary>
    /// Updates role details and its permission configuration.
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionFeatures.Roles, AccessLevels.Write)]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var command = new UpdateRoleCommand(id, request.Name, request.Description, request.PermissionsJson, request.IsActive);
        await _mediator.Send(command);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Deletes a role (cannot delete system roles or roles with assigned users).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionFeatures.Roles, AccessLevels.Write)]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        await _mediator.Send(new DeleteRoleCommand(id));
        return Ok(new { success = true });
    }

    /// <summary>
    /// Assigns a role to a specific team member.
    /// </summary>
    [HttpPost("assign")]
    [RequirePermission(PermissionFeatures.Users, AccessLevels.Write)]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
    {
        await _mediator.Send(new AssignRoleToUserCommand(request.UserId, request.RoleId));
        return Ok(new { success = true });
    }
}

public record CreateRoleRequest(
    string Name,
    string? Description,
    string? PermissionsJson);

public record UpdateRoleRequest(
    string Name,
    string? Description,
    string? PermissionsJson,
    bool IsActive = true);

public record AssignRoleRequest(
    Guid UserId,
    Guid RoleId);
