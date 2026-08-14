using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackLord.Application.Queries;

namespace TrackLord.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DashboardsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets a complete dashboard overview with KPI metrics, step WIP, client distribution, and active lots.
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetDashboardOverview()
    {
        var overview = await _mediator.Send(new GetDashboardOverviewQuery());
        return Ok(overview);
    }

    /// <summary>
    /// Gets step dashboard showing active lots and WIP quantities at a step.
    /// </summary>
    [HttpGet("step/{stepId:guid}")]
    public async Task<IActionResult> GetStepDashboard(Guid stepId)
    {
        var dashboard = await _mediator.Send(new GetStepDashboardQuery(stepId));
        return Ok(dashboard);
    }

    /// <summary>
    /// Gets factory dashboard overview showing WIP distribution across steps.
    /// </summary>
    [HttpGet("factory/{factoryId:guid}")]
    public async Task<IActionResult> GetFactoryDashboard(Guid factoryId)
    {
        var dashboard = await _mediator.Send(new GetFactoryDashboardQuery(factoryId));
        return Ok(dashboard);
    }
}
