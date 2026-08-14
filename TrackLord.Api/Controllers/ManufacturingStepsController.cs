using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackLord.Api.DTOs;
using TrackLord.Application.Commands;
using TrackLord.Application.Queries;

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
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var steps = await _mediator.Send(new GetAllManufacturingStepsQuery());
        return Ok(steps);
    }

    /// <summary>
    /// Creates a new manufacturing step.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateManufacturingStepCommand command)
    {
        var id = await _mediator.Send(command);
        return Ok(new { id });
    }

    /// <summary>
    /// Updates an existing manufacturing step.
    /// </summary>
    [HttpPut("{id:guid}")]
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
}
