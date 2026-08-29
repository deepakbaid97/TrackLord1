using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackLord.Api.Authorization;
using TrackLord.Api.DTOs;
using TrackLord.Application.Commands;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MovementsController : ControllerBase
{
    private readonly IMediator _mediator;

    public MovementsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Moves lot quantity from one step to another (supports partial & backdated movements).
    /// </summary>
    [HttpPost]
    [RequirePermission(PermissionFeatures.Movements, AccessLevels.Write)]
    public async Task<IActionResult> MoveLot([FromBody] MoveLotCommand command)
    {
        var movementId = await _mediator.Send(command);
        return Ok(new { movementId });
    }

    /// <summary>
    /// Edits a historical movement (triggers full ledger replay recalculation).
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionFeatures.Movements, AccessLevels.Write)]
    public async Task<IActionResult> EditMovement(Guid id, [FromBody] EditMovementRequest request)
    {
        var command = new EditMovementCommand(id, request.FromStepId, request.ToStepId, request.NewQuantity, request.NewMovementDate, request.Remarks, request.ReferenceNumber);
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Reverses a historical movement (creates a soft reversal transaction).
    /// Requires elevated movement reversal permission.
    /// </summary>
    [HttpPost("{id:guid}/reverse")]
    [RequirePermission(PermissionFeatures.MovementReversals, AccessLevels.Write)]
    public async Task<IActionResult> ReverseMovement(Guid id, [FromBody] ReverseMovementRequest request)
    {
        var command = new ReverseMovementCommand(id, request.Reason, request.User);
        var reversalId = await _mediator.Send(command);
        return Ok(new { reversalId });
    }
}
