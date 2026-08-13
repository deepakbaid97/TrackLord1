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
