using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackLord.Api.Authorization;
using TrackLord.Api.DTOs;
using TrackLord.Application.Commands;
using TrackLord.Application.Queries;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ManufacturingStepsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ManufacturingStepsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets all manufacturing steps.
    /// Accessible to any role that can view steps, movements, or production lots.
    /// </summary>
    [HttpGet]
    [RequirePermission(new[] { PermissionFeatures.ManufacturingSteps, PermissionFeatures.Movements, PermissionFeatures.ProductionLots }, AccessLevels.Read)]
    public async Task<IActionResult> GetAll()
    {
        var steps = await _mediator.Send(new GetAllManufacturingStepsQuery());
        return Ok(steps);
    }

    /// <summary>
    /// Creates a new manufacturing step.
    /// </summary>
    [HttpPost]
    [RequirePermission(PermissionFeatures.ManufacturingSteps, AccessLevels.Write)]
    public async Task<IActionResult> Create([FromBody] CreateManufacturingStepCommand command)
    {
        var id = await _mediator.Send(command);
        return Ok(new { id });
    }

    /// <summary>
    /// Updates an existing manufacturing step.
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionFeatures.ManufacturingSteps, AccessLevels.Write)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateManufacturingStepRequest request)
    {
        var command = new UpdateManufacturingStepCommand(
            id,
            request.Name,
            request.SequenceOrder,
            request.IsTerminating,
            request.IsActive);
        await _mediator.Send(command);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Reorders manufacturing steps.
    /// </summary>
    [HttpPut("reorder")]
    [RequirePermission(PermissionFeatures.ManufacturingSteps, AccessLevels.Write)]
    public async Task<IActionResult> Reorder([FromBody] ReorderManufacturingStepsRequest request)
    {
        var command = new ReorderManufacturingStepsCommand(request.Steps);
        await _mediator.Send(command);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Gets lots currently or historically at a manufacturing step with time-travel date filtering.
    /// In the createdAt column, returns the movement creation/effective date into the step.
    /// </summary>
    [HttpGet("{id:guid}/lots")]
    [RequirePermission(new[] { PermissionFeatures.ManufacturingSteps, PermissionFeatures.ProductionLots }, AccessLevels.Read)]
    public async Task<IActionResult> GetLotsAtStep(
        Guid id,
        [FromQuery] DateTime? asOfDate,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? search)
    {
        var result = await _mediator.Send(new GetLotsAtStepQuery(id, asOfDate, fromDate, toDate, search));
        return Ok(result);
    }
}
