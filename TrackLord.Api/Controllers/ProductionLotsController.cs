using System;
using System.Collections.Generic;
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
public class ProductionLotsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductionLotsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets all production lots.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionFeatures.ProductionLots, AccessLevels.Read)]
    public async Task<IActionResult> GetAll()
    {
        var lots = await _mediator.Send(new GetAllProductionLotsQuery());
        return Ok(lots);
    }

    /// <summary>
    /// Creates a new Production Lot with an immutable Lot Number.
    /// </summary>
    [HttpPost]
    [RequirePermission(PermissionFeatures.ProductionLots, AccessLevels.Write)]
    public async Task<IActionResult> Create([FromBody] CreateProductionLotCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetWip), new { id }, new { id });
    }

    /// <summary>
    /// Imports multiple production lots from a parsed file structure.
    /// </summary>
    [HttpPost("import")]
    [RequirePermission(PermissionFeatures.Imports, AccessLevels.Write)]
    public async Task<IActionResult> Import([FromBody] ImportProductionLotsCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Gets current Work-In-Progress (WIP) balances per step for a production lot.
    /// </summary>
    [HttpGet("{id:guid}/wip")]
    [RequirePermission(PermissionFeatures.ProductionLots, AccessLevels.Read)]
    public async Task<IActionResult> GetWip(Guid id)
    {
        var wip = await _mediator.Send(new GetCurrentWipQuery(id));
        return Ok(wip);
    }

    /// <summary>
    /// Gets the complete chronological journey and movement ledger for a production lot.
    /// </summary>
    [HttpGet("{id:guid}/journey")]
    [RequirePermission(PermissionFeatures.ProductionLots, AccessLevels.Read)]
    public async Task<IActionResult> GetJourney(Guid id)
    {
        var journey = await _mediator.Send(new GetLotJourneyQuery(id));
        return Ok(journey);
    }

    /// <summary>
    /// Gets complete raw material consumption traceability for a production lot.
    /// </summary>
    [HttpGet("{id:guid}/material-traceability")]
    [RequirePermission(PermissionFeatures.ProductionLots, AccessLevels.Read)]
    public async Task<IActionResult> GetMaterialTraceability(Guid id)
    {
        var trace = await _mediator.Send(new GetMaterialTraceabilityQuery(id));
        return Ok(trace);
    }

    /// <summary>
    /// Deletes a production lot, reverses its material consumptions, and removes all related movements and records.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionFeatures.ProductionLots, AccessLevels.Delete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteProductionLotCommand(id));
        return Ok(new { success = true });
    }

    /// <summary>
    /// Rejects a quantity of items from a step due to damage/wastage and returns them to target step.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [RequirePermission(PermissionFeatures.RejectionRecords, AccessLevels.Write)]
    public async Task<IActionResult> RejectItems(Guid id, [FromBody] RejectItemsRequest request)
    {
        var command = new RejectItemsCommand(
            id,
            request.StepId,
            request.QuantityRejected,
            request.Reason,
            request.RejectionDate,
            request.TargetStepId,
            request.ClientId,
            null,
            request.CreatedBy);
        var rejectionId = await _mediator.Send(command);
        return Ok(new { rejectionId });
    }

    /// <summary>
    /// Gets journeys for multiple production lots in a single batch request.
    /// Eliminates N+1 individual journey calls from the frontend.
    /// </summary>
    [HttpPost("journeys/batch")]
    [RequirePermission(PermissionFeatures.ProductionLots, AccessLevels.Read)]
    public async Task<IActionResult> GetBatchJourneys([FromBody] BatchLotJourneysRequest request)
    {
        if (request?.LotIds == null || request.LotIds.Count == 0)
            return Ok(new Dictionary<Guid, object>());

        var journeys = await _mediator.Send(new GetBatchLotJourneysQuery(request.LotIds));
        return Ok(journeys);
    }
}
