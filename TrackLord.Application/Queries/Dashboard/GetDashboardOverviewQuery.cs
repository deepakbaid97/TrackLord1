using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.Services;

namespace TrackLord.Application.Queries;

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
        var activeLots = await _lotRepository.GetByStatusAsync(LotStatus.Active, cancellationToken);
        var activeLotIds = activeLots.Select(l => l.Id).ToList();

        var allMovements = await _movementRepository.GetMovementsForLotsAsync(activeLotIds, cancellationToken);
        var movementsLookup = allMovements.ToLookup(m => m.ProductionLotId);

        var steps = (await _stepRepository.GetAllAsync(cancellationToken)).OrderBy(s => s.SequenceOrder).ToList();
        var stepsDict = steps.ToDictionary(s => s.Id);
        var terminatingStepIds = steps.Where(s => s.IsTerminating).Select(s => s.Id).ToHashSet() as IReadOnlySet<Guid>;

        var totalClients = await _clientRepository.GetCountAsync(cancellationToken);
        var totalMaterialLots = await _materialLotRepository.GetCountAsync(cancellationToken);
        var clients = await _clientRepository.GetAllAsync(cancellationToken);
        var clientsDict = clients.ToDictionary(c => c.Id);

        var stepWipTotals = steps.ToDictionary(s => s.Id, _ => 0m);
        var stepLotCounts = steps.ToDictionary(s => s.Id, _ => 0);
        var activeLotSummaries = new List<DashboardActiveLotDto>();
        decimal totalTerminatedQty = 0m;
        int nonTerminatedLotCount = 0;

        var clientAggregation = new Dictionary<Guid, (HashSet<Guid> lots, decimal totalQty)>();

        foreach (var lot in activeLots)
        {
            var movements = movementsLookup[lot.Id].ToList();
            var snapshot = _replayEngine.ReplayLedger(lot.Id, movements, terminatingStepIds);

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

            totalTerminatedQty += snapshot.TotalTerminatedQuantity;

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

        var stepSummaries = steps.Select(s => new DashboardStepSummaryDto(
            s.Id,
            s.Code,
            s.Name,
            stepWipTotals[s.Id],
            stepLotCounts[s.Id],
            s.IsTerminating
        )).ToList();

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

        activeLotSummaries = activeLotSummaries
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToList();

        var totalActiveQty = activeLotSummaries.Sum(l => l.CurrentWipQuantity);

        return new DashboardOverviewDto(
            nonTerminatedLotCount,
            totalActiveQty,
            totalTerminatedQty,
            totalClients,
            totalMaterialLots,
            stepSummaries,
            clientSummaries,
            activeLotSummaries);
    }
}
