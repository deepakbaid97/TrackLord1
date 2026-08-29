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
public class MaterialConsumptionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public MaterialConsumptionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets all raw material lots.
    /// </summary>
    [HttpGet("lots")]
    [RequirePermission(PermissionFeatures.MaterialLots, AccessLevels.Read)]
    public async Task<IActionResult> GetAllMaterialLots()
    {
        var lots = await _mediator.Send(new GetAllMaterialLotsQuery());
        return Ok(lots);
    }

    /// <summary>
    /// Gets detailed consumption history for a specific raw material lot.
    /// </summary>
    [HttpGet("lots/{id:guid}/history")]
    [RequirePermission(PermissionFeatures.MaterialLots, AccessLevels.Read)]
    public async Task<IActionResult> GetMaterialLotHistory(Guid id)
    {
        var history = await _mediator.Send(new GetMaterialLotHistoryQuery(id));
        return Ok(history);
    }

    /// <summary>
    /// Creates a raw material lot.
    /// </summary>
    [HttpPost("lots")]
    [RequirePermission(PermissionFeatures.MaterialLots, AccessLevels.Write)]
    public async Task<IActionResult> CreateMaterialLot([FromBody] CreateMaterialLotCommand command)
    {
        var id = await _mediator.Send(command);
        return Ok(new { id });
    }

    /// <summary>
    /// Imports multiple raw material lots from a parsed file structure.
    /// </summary>
    [HttpPost("lots/import")]
    [RequirePermission(PermissionFeatures.Imports, AccessLevels.Write)]
    public async Task<IActionResult> ImportMaterialLots([FromBody] ImportMaterialLotsCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Updates a raw material lot.
    /// </summary>
    [HttpPut("lots/{id:guid}")]
    [RequirePermission(PermissionFeatures.MaterialLots, AccessLevels.Write)]
    public async Task<IActionResult> UpdateMaterialLot(Guid id, [FromBody] UpdateMaterialLotRequest request)
    {
        var command = new UpdateMaterialLotCommand(id, request.LotNumber, request.InitialQuantity, request.Unit, request.MaterialName, request.UnitRate);
        await _mediator.Send(command);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Deletes a raw material lot.
    /// </summary>
    [HttpDelete("lots/{id:guid}")]
    [RequirePermission(PermissionFeatures.MaterialLots, AccessLevels.Delete)]
    public async Task<IActionResult> DeleteMaterialLot(Guid id)
    {
        await _mediator.Send(new DeleteMaterialLotCommand(id));
        return Ok(new { success = true });
    }

    /// <summary>
    /// Consumes raw material lot for a production lot at a specific step.
    /// </summary>
    [HttpPost("consume")]
    [RequirePermission(PermissionFeatures.MaterialConsumptions, AccessLevels.Write)]
    public async Task<IActionResult> ConsumeMaterial([FromBody] ConsumeMaterialCommand command)
    {
        var consumptionId = await _mediator.Send(command);
        return Ok(new { consumptionId });
    }

    /// <summary>
    /// Edits an existing raw material consumption record.
    /// </summary>
    [HttpPut("{id:guid}")]
    [HttpPut("consume/{id:guid}")]
    [RequirePermission(PermissionFeatures.MaterialConsumptions, AccessLevels.Write)]
    public async Task<IActionResult> EditMaterialConsumption(Guid id, [FromBody] EditMaterialConsumptionRequest request)
    {
        var command = new EditMaterialConsumptionCommand(id, request.QuantityConsumed, request.Remarks);
        await _mediator.Send(command);
        return Ok(new { success = true });
    }
}
