using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.Services;

namespace TrackLord.Application.Queries;

// 1. Read Models & DTOs
public record StepWipDto(Guid StepId, string StepCode, string StepName, decimal Quantity);

public record WipStatusDto(
    Guid LotId,
    string LotNumber,
    string Status,
    decimal TotalInitialQuantity,
    decimal TotalCurrentWipQuantity,
    IReadOnlyList<StepWipDto> CurrentStepBalances);

public record MovementTimelineDto(
    Guid MovementId,
    string? FromStepName,
    string ToStepName,
    decimal Quantity,
    string MovementType,
    DateTime MovementDate,
    DateTime CreatedAtUtc,
    string? ClientName,
    string? CreatedBy,
    string? Remarks,
    bool IsReversed);

public record LotJourneyDto(
    Guid LotId,
    string LotNumber,
    string Status,
    DateTime CreatedAtUtc,
    IReadOnlyList<MovementTimelineDto> MovementHistory,
    IReadOnlyList<StepWipDto> ActiveBalances);

public record MaterialConsumptionTraceDto(
    Guid MaterialLotId,
    string MaterialLotNumber,
    Guid StepId,
    string StepName,
    decimal QuantityConsumed,
    string Unit,
    DateTime ConsumptionDate);

public record MaterialTraceabilityDto(
    Guid ProductionLotId,
    string ProductionLotNumber,
    IReadOnlyList<MaterialConsumptionTraceDto> Consumptions);

public record StepDashboardLotDto(
    Guid LotId,
    string LotNumber,
    decimal CurrentQuantityAtStep);

public record StepDashboardDto(
    Guid StepId,
    string StepCode,
    string StepName,
    decimal TotalQuantityAtStep,
    int ActiveLotsCount,
    IReadOnlyList<StepDashboardLotDto> LotsAtStep);

public record FactoryStepSummaryDto(
    Guid StepId,
    string StepCode,
    string StepName,
    decimal TotalWipQuantity);

public record FactoryDashboardDto(
    Guid FactoryId,
    decimal TotalFactoryWip,
    IReadOnlyList<FactoryStepSummaryDto> Steps);

public record ProductionLotSummaryDto(
    Guid Id,
    string LotNumber,
    decimal InitialQuantity,
    string Status,
    DateTime CreatedAtUtc);

public record ManufacturingStepDto(
    Guid Id,
    string Code,
    string Name,
    int SequenceOrder,
    Guid FactoryId);

public record MaterialLotDto(
    Guid Id,
    string LotNumber,
    Guid MaterialItemId,
    string? MaterialName,
    decimal InitialQuantity,
    decimal RemainingQuantity,
    string Unit);


// 2. Queries & Handlers

// GetCurrentWipQuery
public record GetCurrentWipQuery(Guid ProductionLotId) : IRequest<WipStatusDto>;

public class GetCurrentWipQueryHandler : IRequestHandler<GetCurrentWipQuery, WipStatusDto>
{
    private readonly IProductionLotRepository _lotRepository;
    private readonly IManufacturingMovementRepository _movementRepository;
    private readonly IManufacturingStepRepository _stepRepository;
    private readonly ILedgerReplayEngine _replayEngine;

    public GetCurrentWipQueryHandler(
        IProductionLotRepository lotRepository,
        IManufacturingMovementRepository movementRepository,
        IManufacturingStepRepository stepRepository,
        ILedgerReplayEngine replayEngine)
    {
        _lotRepository = lotRepository;
        _movementRepository = movementRepository;
        _stepRepository = stepRepository;
        _replayEngine = replayEngine;
    }

    public async Task<WipStatusDto> Handle(GetCurrentWipQuery request, CancellationToken cancellationToken)
    {
        var lot = await _lotRepository.GetByIdAsync(request.ProductionLotId, cancellationToken);
        if (lot is null) throw new DomainException($"Production lot ID '{request.ProductionLotId}' not found.");

        var movements = await _movementRepository.GetMovementsForLotAsync(request.ProductionLotId, cancellationToken);
        var steps = (await _stepRepository.GetAllAsync(cancellationToken)).ToDictionary(s => s.Id);

        var snapshot = _replayEngine.ReplayLedger(request.ProductionLotId, movements);

        var stepWips = snapshot.StepBalances.Select(sb =>
        {
            steps.TryGetValue(sb.Key, out var step);
            return new StepWipDto(sb.Key, step?.Code ?? "UNKNOWN", step?.Name ?? "Unknown Step", sb.Value);
        }).ToList();

        return new WipStatusDto(
            lot.Id,
            lot.LotNumber.Value,
            lot.Status.ToString(),
            lot.InitialQuantity.Value,
            snapshot.TotalActiveQuantity,
            stepWips);
    }
}

// GetLotJourneyQuery
public record GetLotJourneyQuery(Guid ProductionLotId) : IRequest<LotJourneyDto>;

public class GetLotJourneyQueryHandler : IRequestHandler<GetLotJourneyQuery, LotJourneyDto>
{
    private readonly IProductionLotRepository _lotRepository;
    private readonly IManufacturingMovementRepository _movementRepository;
    private readonly IManufacturingStepRepository _stepRepository;
    private readonly ILedgerReplayEngine _replayEngine;

    public GetLotJourneyQueryHandler(
        IProductionLotRepository lotRepository,
        IManufacturingMovementRepository movementRepository,
        IManufacturingStepRepository stepRepository,
        ILedgerReplayEngine replayEngine)
    {
        _lotRepository = lotRepository;
        _movementRepository = movementRepository;
        _stepRepository = stepRepository;
        _replayEngine = replayEngine;
    }

    public async Task<LotJourneyDto> Handle(GetLotJourneyQuery request, CancellationToken cancellationToken)
    {
        var lot = await _lotRepository.GetByIdAsync(request.ProductionLotId, cancellationToken);
        if (lot is null) throw new DomainException($"Production lot ID '{request.ProductionLotId}' not found.");

        var movements = await _movementRepository.GetMovementsForLotAsync(request.ProductionLotId, cancellationToken);
        var steps = (await _stepRepository.GetAllAsync(cancellationToken)).ToDictionary(s => s.Id);

        var timeline = movements.Select(m =>
        {
            string? fromName = m.FromStepId.HasValue && steps.TryGetValue(m.FromStepId.Value, out var fs) ? fs.Name : null;
            string toName = steps.TryGetValue(m.ToStepId, out var ts) ? ts.Name : "Unknown Step";

            return new MovementTimelineDto(
                m.Id,
                fromName,
                toName,
                m.Quantity,
                m.Type.ToString(),
                m.MovementDate,
                m.CreatedAtUtc,
                m.ClientName,
                m.CreatedBy,
                m.Remarks,
                m.IsReversed);
        }).ToList();

        var snapshot = _replayEngine.ReplayLedger(request.ProductionLotId, movements);
        var activeWips = snapshot.StepBalances.Select(sb =>
        {
            steps.TryGetValue(sb.Key, out var step);
            return new StepWipDto(sb.Key, step?.Code ?? "UNKNOWN", step?.Name ?? "Unknown Step", sb.Value);
        }).ToList();

        return new LotJourneyDto(
            lot.Id,
            lot.LotNumber.Value,
            lot.Status.ToString(),
            lot.CreatedAtUtc,
            timeline,
            activeWips);
    }
}

// GetMaterialTraceabilityQuery
public record GetMaterialTraceabilityQuery(Guid ProductionLotId) : IRequest<MaterialTraceabilityDto>;

public class GetMaterialTraceabilityQueryHandler : IRequestHandler<GetMaterialTraceabilityQuery, MaterialTraceabilityDto>
{
    private readonly IProductionLotRepository _lotRepository;
    private readonly IMaterialConsumptionRepository _consumptionRepository;
    private readonly IMaterialLotRepository _materialLotRepository;
    private readonly IManufacturingStepRepository _stepRepository;

    public GetMaterialTraceabilityQueryHandler(
        IProductionLotRepository lotRepository,
        IMaterialConsumptionRepository consumptionRepository,
        IMaterialLotRepository materialLotRepository,
        IManufacturingStepRepository stepRepository)
    {
        _lotRepository = lotRepository;
        _consumptionRepository = consumptionRepository;
        _materialLotRepository = materialLotRepository;
        _stepRepository = stepRepository;
    }

    public async Task<MaterialTraceabilityDto> Handle(GetMaterialTraceabilityQuery request, CancellationToken cancellationToken)
    {
        var lot = await _lotRepository.GetByIdAsync(request.ProductionLotId, cancellationToken);
        if (lot is null) throw new DomainException($"Production lot ID '{request.ProductionLotId}' not found.");

        var consumptions = await _consumptionRepository.GetByProductionLotIdAsync(request.ProductionLotId, cancellationToken);
        var steps = (await _stepRepository.GetAllAsync(cancellationToken)).ToDictionary(s => s.Id);

        var traces = new List<MaterialConsumptionTraceDto>();
        foreach (var c in consumptions)
        {
            var matLot = await _materialLotRepository.GetByIdAsync(c.MaterialLotId, cancellationToken);
            steps.TryGetValue(c.StepId, out var step);

            traces.Add(new MaterialConsumptionTraceDto(
                c.MaterialLotId,
                matLot?.LotNumber.Value ?? "UNKNOWN",
                c.StepId,
                step?.Name ?? "Unknown Step",
                c.QuantityConsumed,
                c.Unit.ToString(),
                c.ConsumptionDate));
        }

        return new MaterialTraceabilityDto(lot.Id, lot.LotNumber.Value, traces);
    }
}

// GetStepDashboardQuery
public record GetStepDashboardQuery(Guid StepId) : IRequest<StepDashboardDto>;

public class GetStepDashboardQueryHandler : IRequestHandler<GetStepDashboardQuery, StepDashboardDto>
{
    private readonly IManufacturingStepRepository _stepRepository;
    private readonly IProductionLotRepository _lotRepository;
    private readonly IManufacturingMovementRepository _movementRepository;
    private readonly ILedgerReplayEngine _replayEngine;

    public GetStepDashboardQueryHandler(
        IManufacturingStepRepository stepRepository,
        IProductionLotRepository lotRepository,
        IManufacturingMovementRepository movementRepository,
        ILedgerReplayEngine replayEngine)
    {
        _stepRepository = stepRepository;
        _lotRepository = lotRepository;
        _movementRepository = movementRepository;
        _replayEngine = replayEngine;
    }

    public async Task<StepDashboardDto> Handle(GetStepDashboardQuery request, CancellationToken cancellationToken)
    {
        var step = await _stepRepository.GetByIdAsync(request.StepId, cancellationToken);
        if (step is null) throw new DomainException($"Step ID '{request.StepId}' not found.");

        var lots = await _lotRepository.GetAllAsync(cancellationToken);
        var lotsAtStep = new List<StepDashboardLotDto>();

        foreach (var lot in lots)
        {
            var movements = await _movementRepository.GetMovementsForLotAsync(lot.Id, cancellationToken);
            var snapshot = _replayEngine.ReplayLedger(lot.Id, movements);

            if (snapshot.StepBalances.TryGetValue(request.StepId, out var qtyAtStep) && qtyAtStep > 0)
            {
                lotsAtStep.Add(new StepDashboardLotDto(lot.Id, lot.LotNumber.Value, qtyAtStep));
            }
        }

        var totalQty = lotsAtStep.Sum(l => l.CurrentQuantityAtStep);

        return new StepDashboardDto(
            step.Id,
            step.Code,
            step.Name,
            totalQty,
            lotsAtStep.Count,
            lotsAtStep);
    }
}

// GetFactoryDashboardQuery
public record GetFactoryDashboardQuery(Guid FactoryId) : IRequest<FactoryDashboardDto>;

public class GetFactoryDashboardQueryHandler : IRequestHandler<GetFactoryDashboardQuery, FactoryDashboardDto>
{
    private readonly IManufacturingStepRepository _stepRepository;
    private readonly IProductionLotRepository _lotRepository;
    private readonly IManufacturingMovementRepository _movementRepository;
    private readonly ILedgerReplayEngine _replayEngine;

    public GetFactoryDashboardQueryHandler(
        IManufacturingStepRepository stepRepository,
        IProductionLotRepository lotRepository,
        IManufacturingMovementRepository movementRepository,
        ILedgerReplayEngine replayEngine)
    {
        _stepRepository = stepRepository;
        _lotRepository = lotRepository;
        _movementRepository = movementRepository;
        _replayEngine = replayEngine;
    }

    public async Task<FactoryDashboardDto> Handle(GetFactoryDashboardQuery request, CancellationToken cancellationToken)
    {
        var steps = await _stepRepository.GetAllAsync(cancellationToken);
        var factorySteps = steps.Where(s => s.FactoryId == request.FactoryId).ToList();

        var lots = await _lotRepository.GetAllAsync(cancellationToken);
        var stepQuantities = factorySteps.ToDictionary(s => s.Id, s => 0m);

        foreach (var lot in lots)
        {
            var movements = await _movementRepository.GetMovementsForLotAsync(lot.Id, cancellationToken);
            var snapshot = _replayEngine.ReplayLedger(lot.Id, movements);

            foreach (var (stepId, qty) in snapshot.StepBalances)
            {
                if (stepQuantities.ContainsKey(stepId))
                {
                    stepQuantities[stepId] += qty;
                }
            }
        }

        var stepSummaries = factorySteps.Select(s => new FactoryStepSummaryDto(
            s.Id,
            s.Code,
            s.Name,
            stepQuantities[s.Id])).ToList();

        var totalFactoryWip = stepSummaries.Sum(s => s.TotalWipQuantity);

        return new FactoryDashboardDto(request.FactoryId, totalFactoryWip, stepSummaries);
    }
}

// GetAllProductionLotsQuery
public record GetAllProductionLotsQuery : IRequest<IReadOnlyList<ProductionLotSummaryDto>>;

public class GetAllProductionLotsQueryHandler : IRequestHandler<GetAllProductionLotsQuery, IReadOnlyList<ProductionLotSummaryDto>>
{
    private readonly IProductionLotRepository _lotRepository;

    public GetAllProductionLotsQueryHandler(IProductionLotRepository lotRepository)
    {
        _lotRepository = lotRepository;
    }

    public async Task<IReadOnlyList<ProductionLotSummaryDto>> Handle(GetAllProductionLotsQuery request, CancellationToken cancellationToken)
    {
        var lots = await _lotRepository.GetAllAsync(cancellationToken);
        return lots.Select(l => new ProductionLotSummaryDto(
            l.Id,
            l.LotNumber.Value,
            l.InitialQuantity.Value,
            l.Status.ToString(),
            l.CreatedAtUtc
        )).OrderByDescending(l => l.CreatedAtUtc).ToList();
    }
}

// GetAllManufacturingStepsQuery
public record GetAllManufacturingStepsQuery : IRequest<IReadOnlyList<ManufacturingStepDto>>;

public class GetAllManufacturingStepsQueryHandler : IRequestHandler<GetAllManufacturingStepsQuery, IReadOnlyList<ManufacturingStepDto>>
{
    private readonly IManufacturingStepRepository _stepRepository;

    public GetAllManufacturingStepsQueryHandler(IManufacturingStepRepository stepRepository)
    {
        _stepRepository = stepRepository;
    }

    public async Task<IReadOnlyList<ManufacturingStepDto>> Handle(GetAllManufacturingStepsQuery request, CancellationToken cancellationToken)
    {
        var steps = await _stepRepository.GetAllAsync(cancellationToken);
        return steps.Select(s => new ManufacturingStepDto(
            s.Id,
            s.Code,
            s.Name,
            s.SequenceOrder,
            s.FactoryId
        )).OrderBy(s => s.SequenceOrder).ToList();
    }
}

// GetAllMaterialLotsQuery
public record GetAllMaterialLotsQuery : IRequest<IReadOnlyList<MaterialLotDto>>;

public class GetAllMaterialLotsQueryHandler : IRequestHandler<GetAllMaterialLotsQuery, IReadOnlyList<MaterialLotDto>>
{
    private readonly IMaterialLotRepository _materialLotRepository;

    public GetAllMaterialLotsQueryHandler(IMaterialLotRepository materialLotRepository)
    {
        _materialLotRepository = materialLotRepository;
    }

    public async Task<IReadOnlyList<MaterialLotDto>> Handle(GetAllMaterialLotsQuery request, CancellationToken cancellationToken)
    {
        var lots = await _materialLotRepository.GetAllAsync(cancellationToken);
        return lots.Select(m => new MaterialLotDto(
            m.Id,
            m.LotNumber.Value,
            m.MaterialItemId,
            m.MaterialName,
            m.InitialQuantity.Value,
            m.CurrentQuantity.Value,
            m.InitialQuantity.Unit.ToString()
        )).OrderByDescending(m => m.Id).ToList();
    }
}
