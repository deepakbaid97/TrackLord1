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

        var activeLots = await _lotRepository.GetByStatusAsync(LotStatus.Active, cancellationToken);
        var activeLotIds = activeLots.Select(l => l.Id).ToList();

        var allMovements = await _movementRepository.GetMovementsForLotsAsync(activeLotIds, cancellationToken);
        var movementsLookup = allMovements.ToLookup(m => m.ProductionLotId);

        var lotsAtStep = new List<StepDashboardLotDto>();

        foreach (var lot in activeLots)
        {
            var movements = movementsLookup[lot.Id].ToList();
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
