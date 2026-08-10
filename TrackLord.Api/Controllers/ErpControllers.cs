using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackLord.Application.Commands;
using TrackLord.Application.Queries;

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
    public async Task<IActionResult> GetAll()
    {
        var lots = await _mediator.Send(new GetAllProductionLotsQuery());
        return Ok(lots);
    }

    /// <summary>
    /// Creates a new Production Lot with an immutable Lot Number.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductionLotCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetWip), new { id }, new { id });
    }

    /// <summary>
    /// Imports multiple production lots from a parsed file structure.
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportProductionLotsCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Gets current Work-In-Progress (WIP) balances per step for a production lot.
    /// </summary>
    [HttpGet("{id:guid}/wip")]
    public async Task<IActionResult> GetWip(Guid id)
    {
        var wip = await _mediator.Send(new GetCurrentWipQuery(id));
        return Ok(wip);
    }

    /// <summary>
    /// Gets the complete chronological journey and movement ledger for a production lot.
    /// </summary>
    [HttpGet("{id:guid}/journey")]
    public async Task<IActionResult> GetJourney(Guid id)
    {
        var journey = await _mediator.Send(new GetLotJourneyQuery(id));
        return Ok(journey);
    }

    /// <summary>
    /// Gets complete raw material consumption traceability for a production lot.
    /// </summary>
    [HttpGet("{id:guid}/material-traceability")]
    public async Task<IActionResult> GetMaterialTraceability(Guid id)
    {
        var trace = await _mediator.Send(new GetMaterialTraceabilityQuery(id));
        return Ok(trace);
    }

    /// <summary>
    /// Rejects a quantity of items from a step due to damage/wastage and returns them to target step.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
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
}

public record RejectItemsRequest(
    Guid StepId,
    Guid? TargetStepId,
    decimal QuantityRejected,
    string Reason,
    DateTime RejectionDate,
    Guid? ClientId,
    string? CreatedBy);

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
    public async Task<IActionResult> MoveLot([FromBody] MoveLotCommand command)
    {
        var movementId = await _mediator.Send(command);
        return Ok(new { movementId });
    }

    /// <summary>
    /// Edits a historical movement (triggers full ledger replay recalculation).
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> EditMovement(Guid id, [FromBody] EditMovementRequest request)
    {
        var command = new EditMovementCommand(id, request.FromStepId, request.ToStepId, request.NewQuantity, request.NewMovementDate, request.Remarks, request.ReferenceNumber);
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Reverses a historical movement (creates a soft reversal transaction).
    /// </summary>
    [HttpPost("{id:guid}/reverse")]
    public async Task<IActionResult> ReverseMovement(Guid id, [FromBody] ReverseMovementRequest request)
    {
        var command = new ReverseMovementCommand(id, request.Reason, request.User);
        var reversalId = await _mediator.Send(command);
        return Ok(new { reversalId });
    }
}

public record EditMovementRequest(Guid? FromStepId, Guid ToStepId, decimal NewQuantity, DateTime NewMovementDate, string? Remarks, string? ReferenceNumber);
public record ReverseMovementRequest(string Reason, string? User);

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

public record UpdateManufacturingStepRequest(string Name, int SequenceOrder, bool IsTerminating, bool IsActive);

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
    public async Task<IActionResult> GetAllMaterialLots()
    {
        var lots = await _mediator.Send(new GetAllMaterialLotsQuery());
        return Ok(lots);
    }

    /// <summary>
    /// Creates a raw material lot.
    /// </summary>
    [HttpPost("lots")]
    public async Task<IActionResult> CreateMaterialLot([FromBody] CreateMaterialLotCommand command)
    {
        var id = await _mediator.Send(command);
        return Ok(new { id });
    }

    /// <summary>
    /// Consumes raw material lot for a production lot at a specific step.
    /// </summary>
    [HttpPost("consume")]
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
    public async Task<IActionResult> EditMaterialConsumption(Guid id, [FromBody] EditMaterialConsumptionRequest request)
    {
        var command = new EditMaterialConsumptionCommand(id, request.QuantityConsumed, request.Remarks);
        await _mediator.Send(command);
        return Ok(new { success = true });
    }
}

public record EditMaterialConsumptionRequest(decimal QuantityConsumed, string? Remarks);

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
