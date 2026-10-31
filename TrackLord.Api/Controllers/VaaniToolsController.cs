using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackLord.Api.Authorization;
using TrackLord.Application.Queries;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/vaani-tools")]
public sealed class VaaniToolsController : ControllerBase
{
    private readonly IMediator _mediator;

    public VaaniToolsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("dashboard-overview")]
    [VaaniVoiceToken]
    [RequirePermission(PermissionFeatures.Dashboard, AccessLevels.Read)]
    public async Task<IActionResult> GetDashboardOverview([FromBody] VaaniVoiceToolRequest request)
    {
        _ = request;
        return Ok(await _mediator.Send(new GetDashboardOverviewQuery()));
    }

    [HttpPost("production-lots")]
    [VaaniVoiceToken]
    [RequirePermission(PermissionFeatures.ProductionLots, AccessLevels.Read)]
    public async Task<IActionResult> GetProductionLots([FromBody] VaaniVoiceToolRequest request)
    {
        _ = request;
        return Ok(await _mediator.Send(new GetAllProductionLotsQuery()));
    }

    [HttpPost("production-lot-wip")]
    [VaaniVoiceToken]
    [RequirePermission(PermissionFeatures.ProductionLots, AccessLevels.Read)]
    public async Task<IActionResult> GetProductionLotWip([FromBody] VaaniLotToolRequest request)
        => Ok(await _mediator.Send(new GetCurrentWipQuery(request.LotId)));

    [HttpPost("production-lot-journey")]
    [VaaniVoiceToken]
    [RequirePermission(PermissionFeatures.ProductionLots, AccessLevels.Read)]
    public async Task<IActionResult> GetProductionLotJourney([FromBody] VaaniLotToolRequest request)
        => Ok(await _mediator.Send(new GetLotJourneyQuery(request.LotId)));

    [HttpPost("material-traceability")]
    [VaaniVoiceToken]
    [RequirePermission(PermissionFeatures.ProductionLots, AccessLevels.Read)]
    public async Task<IActionResult> GetMaterialTraceability([FromBody] VaaniLotToolRequest request)
        => Ok(await _mediator.Send(new GetMaterialTraceabilityQuery(request.LotId)));

    [HttpPost("manufacturing-steps")]
    [VaaniVoiceToken]
    [RequirePermission(
        new[] { PermissionFeatures.ManufacturingSteps, PermissionFeatures.Movements, PermissionFeatures.ProductionLots },
        AccessLevels.Read)]
    public async Task<IActionResult> GetManufacturingSteps([FromBody] VaaniVoiceToolRequest request)
    {
        _ = request;
        return Ok(await _mediator.Send(new GetAllManufacturingStepsQuery()));
    }

    [HttpPost("material-lots")]
    [VaaniVoiceToken]
    [RequirePermission(PermissionFeatures.MaterialLots, AccessLevels.Read)]
    public async Task<IActionResult> GetMaterialLots([FromBody] VaaniVoiceToolRequest request)
    {
        _ = request;
        return Ok(await _mediator.Send(new GetAllMaterialLotsQuery()));
    }

    [HttpPost("clients")]
    [VaaniVoiceToken]
    [RequirePermission(
        new[] { PermissionFeatures.Clients, PermissionFeatures.Movements, PermissionFeatures.ProductionLots },
        AccessLevels.Read)]
    public async Task<IActionResult> GetClients([FromBody] VaaniVoiceToolRequest request)
    {
        _ = request;
        return Ok(await _mediator.Send(new GetAllClientsQuery()));
    }
}

public sealed record VaaniVoiceToolRequest(
    [Required, StringLength(4096, MinimumLength = 1)] string VoiceToken) : IVaaniVoiceTokenRequest;

public sealed record VaaniLotToolRequest(
    [Required, StringLength(4096, MinimumLength = 1)] string VoiceToken,
    [Required] Guid LotId) : IVaaniVoiceTokenRequest;
