using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Application.Queries.Dashboard.Strategies;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Common;
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
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILedgerReplayEngine _replayEngine;

    public GetDashboardOverviewQueryHandler(
        IProductionLotRepository lotRepository,
        IManufacturingStepRepository stepRepository,
        IManufacturingMovementRepository movementRepository,
        IMaterialLotRepository materialLotRepository,
        IClientRepository clientRepository,
        ITenantRepository tenantRepository,
        ITenantContext tenantContext,
        ILedgerReplayEngine replayEngine)
    {
        _lotRepository = lotRepository;
        _stepRepository = stepRepository;
        _movementRepository = movementRepository;
        _materialLotRepository = materialLotRepository;
        _clientRepository = clientRepository;
        _tenantRepository = tenantRepository;
        _tenantContext = tenantContext;
        _replayEngine = replayEngine;
    }

    public async Task<DashboardOverviewDto> Handle(GetDashboardOverviewQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(_tenantContext.TenantId, cancellationToken);
        TenantSettings? settings = null;
        if (tenant != null && !string.IsNullOrWhiteSpace(tenant.SettingsJson))
        {
            try
            {
                settings = JsonSerializer.Deserialize<TenantSettings>(tenant.SettingsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { }
        }
        settings ??= new TenantSettings();

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
        var lotSnapshots = new Dictionary<Guid, LotLedgerSnapshot>();
        decimal totalTerminatedQty = 0m;
        int nonTerminatedLotCount = 0;

        foreach (var lot in activeLots)
        {
            var movements = movementsLookup[lot.Id].ToList();
            var snapshot = _replayEngine.ReplayLedger(lot.Id, movements, terminatingStepIds);
            lotSnapshots[lot.Id] = snapshot;

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

                // Primary step prefers active, non-terminating steps with largest quantity
                if (stepsDict.TryGetValue(stepId, out var s) && s.IsActive && !s.IsTerminating)
                {
                    if (qty > maxStepQty)
                    {
                        maxStepQty = qty;
                        primaryStepName = s.Name;
                    }
                }
            }

            if (primaryStepName == "—" && snapshot.StepBalances.Any(sb => sb.Value > 0))
            {
                var topStep = snapshot.StepBalances.OrderByDescending(sb => sb.Value).First();
                primaryStepName = stepsDict.TryGetValue(topStep.Key, out var s) ? s.Name : "Unknown";
            }

            totalTerminatedQty += snapshot.TotalTerminatedQuantity;

            // Only count quantity residing in active, non-terminating manufacturing steps as active WIP
            decimal lotActiveWipQuantity = snapshot.StepBalances
                .Where(sb => stepsDict.TryGetValue(sb.Key, out var s) && s.IsActive && !s.IsTerminating)
                .Sum(sb => sb.Value);

            bool lotHasActiveQuantity = lotActiveWipQuantity > 0;
            if (lotHasActiveQuantity)
            {
                nonTerminatedLotCount++;

                activeLotSummaries.Add(new DashboardActiveLotDto(
                    lot.Id,
                    lot.LotNumber.Value,
                    lot.InitialQuantity.Value,
                    lotActiveWipQuantity,
                    primaryStepName,
                    lot.CreatedAtUtc));
            }
        }

        var stepSummaries = steps.Select(s => new DashboardStepSummaryDto(
            s.Id,
            s.Code,
            s.Name,
            stepWipTotals[s.Id],
            stepLotCounts[s.Id],
            s.IsTerminating,
            s.IsActive
        )).ToList();

        // Delegate Client Summary Aggregation to Tenant's Strategy
        var clientStrategy = ClientAggregationStrategyFactory.Create(settings.ClientMode);
        var clientSummaries = clientStrategy.AggregateClientSummaries(activeLots, movementsLookup, clientsDict, lotSnapshots);

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
