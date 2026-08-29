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
public class ClientsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ClientsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets all registered clients.
    /// Accessible to any role that can view clients, movements, or production lots.
    /// </summary>
    [HttpGet]
    [RequirePermission(new[] { PermissionFeatures.Clients, PermissionFeatures.Movements, PermissionFeatures.ProductionLots }, AccessLevels.Read)]
    public async Task<IActionResult> GetAll()
    {
        var clients = await _mediator.Send(new GetAllClientsQuery());
        return Ok(clients);
    }

    /// <summary>
    /// Creates a new client.
    /// </summary>
    [HttpPost]
    [RequirePermission(PermissionFeatures.Clients, AccessLevels.Write)]
    public async Task<IActionResult> Create([FromBody] CreateClientRequest request)
    {
        var command = new CreateClientCommand(request.Name, request.PhoneNumber, request.ClientType);
        var id = await _mediator.Send(command);
        return Ok(new { id });
    }
}

public record CreateClientRequest(string Name, string? PhoneNumber, string ClientType);
