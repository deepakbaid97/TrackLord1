using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.Services;

namespace TrackLord.Application.Queries;

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
