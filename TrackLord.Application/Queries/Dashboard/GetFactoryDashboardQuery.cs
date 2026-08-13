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

        var activeLots = await _lotRepository.GetByStatusAsync(LotStatus.Active, cancellationToken);
        var activeLotIds = activeLots.Select(l => l.Id).ToList();

        var allMovements = await _movementRepository.GetMovementsForLotsAsync(activeLotIds, cancellationToken);
        var movementsLookup = allMovements.ToLookup(m => m.ProductionLotId);

        var stepQuantities = factorySteps.ToDictionary(s => s.Id, s => 0m);

        foreach (var lot in activeLots)
        {
            var movements = movementsLookup[lot.Id].ToList();
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

        var totalFactoryWip = factorySteps
            .Where(s => !s.IsTerminating)
            .Sum(s => stepQuantities[s.Id]);

        return new FactoryDashboardDto(request.FactoryId, totalFactoryWip, stepSummaries);
    }
}
