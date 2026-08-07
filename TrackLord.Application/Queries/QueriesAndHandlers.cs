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
    decimal TotalTerminatedQuantity,
    IReadOnlyList<StepWipDto> CurrentStepBalances);

public record MovementTimelineDto(
    Guid MovementId,
    string? FromStepName,
    string ToStepName,
    decimal Quantity,
    string MovementType,
    DateTime MovementDate,
    DateTime CreatedAtUtc,
    Guid? ClientId,
    string? ClientName,
    string? CreatedBy,
    string? Remarks,
    string? ReferenceNumber,
    bool IsReversed);

public record LotJourneyDto(
    Guid LotId,
    string LotNumber,
    string Status,
    DateTime CreatedAtUtc,
    IReadOnlyList<MovementTimelineDto> MovementHistory,
    IReadOnlyList<StepWipDto> ActiveBalances);

public record MaterialConsumptionTraceDto(
    Guid Id,
    Guid MaterialLotId,
    string MaterialLotNumber,
    string? MaterialName,
    Guid StepId,
    string StepName,
    decimal QuantityConsumed,
    string Unit,
    DateTime ConsumptionDate,
    string? Remarks);

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
    bool IsTerminating,
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
        var terminatingStepIds = steps.Values.Where(s => s.IsTerminating).Select(s => s.Id).ToHashSet() as IReadOnlySet<Guid>;

        var snapshot = _replayEngine.ReplayLedger(request.ProductionLotId, movements, terminatingStepIds);

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
            snapshot.TotalTerminatedQuantity,
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
        var terminatingStepIds = steps.Values.Where(s => s.IsTerminating).Select(s => s.Id).ToHashSet() as IReadOnlySet<Guid>;

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
                m.ClientId,
                m.ClientName,
                m.CreatedBy,
                m.Remarks,
                m.ReferenceNumber,
                m.IsReversed);
        }).ToList();

        var snapshot = _replayEngine.ReplayLedger(request.ProductionLotId, movements, terminatingStepIds);
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
                c.Id,
                c.MaterialLotId,
                matLot?.LotNumber.Value ?? "UNKNOWN",
                matLot?.MaterialName,
                c.StepId,
                step?.Name ?? "Unknown Step",
                c.QuantityConsumed,
                c.Unit.ToString(),
                c.ConsumptionDate,
                c.Remarks));
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

        var allSteps = await _stepRepository.GetAllAsync(cancellationToken);
        var terminatingStepIds = allSteps.Where(s => s.IsTerminating).Select(s => s.Id).ToHashSet() as IReadOnlySet<Guid>;

        var lots = await _lotRepository.GetAllAsync(cancellationToken);
        var lotsAtStep = new List<StepDashboardLotDto>();

        foreach (var lot in lots)
        {
            var movements = await _movementRepository.GetMovementsForLotAsync(lot.Id, cancellationToken);
            var snapshot = _replayEngine.ReplayLedger(lot.Id, movements, terminatingStepIds);

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
        var terminatingStepIds = steps.Where(s => s.IsTerminating).Select(s => s.Id).ToHashSet() as IReadOnlySet<Guid>;

        var lots = await _lotRepository.GetAllAsync(cancellationToken);
        var stepQuantities = factorySteps.ToDictionary(s => s.Id, s => 0m);

        foreach (var lot in lots)
        {
            var movements = await _movementRepository.GetMovementsForLotAsync(lot.Id, cancellationToken);
            var snapshot = _replayEngine.ReplayLedger(lot.Id, movements, terminatingStepIds);

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

        // Exclude terminating step quantities from total factory WIP
        var totalFactoryWip = factorySteps
            .Where(s => !s.IsTerminating)
            .Sum(s => stepQuantities[s.Id]);

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
            s.IsTerminating,
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

// Dashboard Overview DTOs
public record DashboardStepSummaryDto(
    Guid StepId,
    string StepCode,
    string StepName,
    decimal TotalWipQuantity,
    int ActiveLotsCount,
    bool IsTerminating);

public record DashboardClientSummaryDto(
    Guid ClientId,
    string ClientName,
    string ClientType,
    int TotalLotsAssociated,
    decimal TotalQuantityMoved);

public record DashboardActiveLotDto(
    Guid LotId,
    string LotNumber,
    decimal InitialQuantity,
    decimal CurrentWipQuantity,
    string CurrentStepName,
    DateTime CreatedAtUtc);

public record DashboardOverviewDto(
    int TotalActiveLots,
    decimal TotalActiveQuantity,
    decimal TotalTerminatedQuantity,
    int TotalClients,
    int TotalMaterialLots,
    IReadOnlyList<DashboardStepSummaryDto> StepSummaries,
    IReadOnlyList<DashboardClientSummaryDto> ClientSummaries,
    IReadOnlyList<DashboardActiveLotDto> ActiveLotSummaries);

// GetDashboardOverviewQuery
public record GetDashboardOverviewQuery : IRequest<DashboardOverviewDto>;

public class GetDashboardOverviewQueryHandler : IRequestHandler<GetDashboardOverviewQuery, DashboardOverviewDto>
{
    private readonly IProductionLotRepository _lotRepository;
    private readonly IManufacturingStepRepository _stepRepository;
    private readonly IManufacturingMovementRepository _movementRepository;
    private readonly IMaterialLotRepository _materialLotRepository;
    private readonly IClientRepository _clientRepository;
    private readonly ILedgerReplayEngine _replayEngine;

    public GetDashboardOverviewQueryHandler(
        IProductionLotRepository lotRepository,
        IManufacturingStepRepository stepRepository,
        IManufacturingMovementRepository movementRepository,
        IMaterialLotRepository materialLotRepository,
        IClientRepository clientRepository,
        ILedgerReplayEngine replayEngine)
    {
        _lotRepository = lotRepository;
        _stepRepository = stepRepository;
        _movementRepository = movementRepository;
        _materialLotRepository = materialLotRepository;
        _clientRepository = clientRepository;
        _replayEngine = replayEngine;
    }

    public async Task<DashboardOverviewDto> Handle(GetDashboardOverviewQuery request, CancellationToken cancellationToken)
    {
        // 1. Fetch all reference data
        var allLots = await _lotRepository.GetAllAsync(cancellationToken);
        var activeLots = allLots.Where(l => l.Status == LotStatus.Active).ToList();
        var steps = (await _stepRepository.GetAllAsync(cancellationToken)).OrderBy(s => s.SequenceOrder).ToList();
        var stepsDict = steps.ToDictionary(s => s.Id);
        var terminatingStepIds = steps.Where(s => s.IsTerminating).Select(s => s.Id).ToHashSet() as IReadOnlySet<Guid>;
        var clients = await _clientRepository.GetAllAsync(cancellationToken);
        var clientsDict = clients.ToDictionary(c => c.Id);
        var materialLots = await _materialLotRepository.GetAllAsync(cancellationToken);

        // 2. Per-step WIP aggregation + per-lot summaries
        var stepWipTotals = steps.ToDictionary(s => s.Id, _ => 0m);
        var stepLotCounts = steps.ToDictionary(s => s.Id, _ => 0);
        var activeLotSummaries = new List<DashboardActiveLotDto>();
        decimal totalTerminatedQty = 0m;
        int nonTerminatedLotCount = 0;

        // 3. Per-client aggregation from movements
        // Key: ClientId, Value: (lotsSet, totalQty)
        var clientAggregation = new Dictionary<Guid, (HashSet<Guid> lots, decimal totalQty)>();

        foreach (var lot in activeLots)
        {
            var movements = await _movementRepository.GetMovementsForLotAsync(lot.Id, cancellationToken);
            var snapshot = _replayEngine.ReplayLedger(lot.Id, movements, terminatingStepIds);

            // Aggregate step WIP
            decimal lotWipTotal = 0m;
            string primaryStepName = "—";
            decimal maxStepQty = 0m;

            foreach (var (stepId, qty) in snapshot.StepBalances)
            {
                if (qty <= 0) continue;

                lotWipTotal += qty;

                if (stepWipTotals.ContainsKey(stepId))
                {
                    stepWipTotals[stepId] += qty;
                    stepLotCounts[stepId]++;
                }

                if (qty > maxStepQty)
                {
                    maxStepQty = qty;
                    primaryStepName = stepsDict.TryGetValue(stepId, out var s) ? s.Name : "Unknown";
                }
            }

            // Track terminated vs active quantities
            totalTerminatedQty += snapshot.TotalTerminatedQuantity;

            // A lot is considered "active" (non-terminated) if it has any non-terminated quantity
            bool lotHasActiveQuantity = snapshot.TotalActiveQuantity > 0;
            if (lotHasActiveQuantity)
            {
                nonTerminatedLotCount++;
            }

            activeLotSummaries.Add(new DashboardActiveLotDto(
                lot.Id,
                lot.LotNumber.Value,
                lot.InitialQuantity.Value,
                snapshot.TotalActiveQuantity,
                primaryStepName,
                lot.CreatedAtUtc));

            // Aggregate client data from non-reversed movements
            foreach (var m in movements.Where(m => !m.IsReversed && m.ClientId.HasValue))
            {
                var cid = m.ClientId!.Value;
                if (!clientAggregation.ContainsKey(cid))
                    clientAggregation[cid] = (new HashSet<Guid>(), 0m);

                var (lots, totalQty) = clientAggregation[cid];
                lots.Add(lot.Id);
                clientAggregation[cid] = (lots, totalQty + m.Quantity);
            }
        }

        // 4. Build step summaries
        var stepSummaries = steps.Select(s => new DashboardStepSummaryDto(
            s.Id,
            s.Code,
            s.Name,
            stepWipTotals[s.Id],
            stepLotCounts[s.Id],
            s.IsTerminating
        )).ToList();

        // 5. Build client summaries
        var clientSummaries = clientAggregation
            .Select(kvp =>
            {
                clientsDict.TryGetValue(kvp.Key, out var client);
                return new DashboardClientSummaryDto(
                    kvp.Key,
                    client?.Name ?? "Unknown",
                    client?.ClientType ?? "Unknown",
                    kvp.Value.lots.Count,
                    kvp.Value.totalQty);
            })
            .OrderByDescending(c => c.TotalQuantityMoved)
            .ToList();

        // 6. Sort active lots by creation date desc
        activeLotSummaries = activeLotSummaries
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToList();

        var totalActiveQty = activeLotSummaries.Sum(l => l.CurrentWipQuantity);

        return new DashboardOverviewDto(
            nonTerminatedLotCount,
            totalActiveQty,
            totalTerminatedQty,
            clients.Count,
            materialLots.Count,
            stepSummaries,
            clientSummaries,
            activeLotSummaries);
    }
}
