using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.Services;

namespace TrackLord.Application.Queries;

public record GetBatchLotJourneysQuery(IReadOnlyList<Guid> LotIds) : IRequest<Dictionary<Guid, LotJourneyDto>>;

public class GetBatchLotJourneysQueryHandler : IRequestHandler<GetBatchLotJourneysQuery, Dictionary<Guid, LotJourneyDto>>
{
    private readonly IProductionLotRepository _lotRepository;
    private readonly IManufacturingMovementRepository _movementRepository;
    private readonly IManufacturingStepRepository _stepRepository;
    private readonly ILedgerReplayEngine _replayEngine;

    public GetBatchLotJourneysQueryHandler(
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

    public async Task<Dictionary<Guid, LotJourneyDto>> Handle(GetBatchLotJourneysQuery request, CancellationToken cancellationToken)
    {
        if (request.LotIds == null || request.LotIds.Count == 0)
            return new Dictionary<Guid, LotJourneyDto>();

        // Enforce maximum batch size cap (prevents memory explosion if a client requests thousands of lots)
        var distinctRequestedIds = request.LotIds.Distinct().Take(200).ToList();

        // 1. Single DB call: fetch ONLY the requested lots matching tenant filter (indexed PK seek)
        var lots = await _lotRepository.GetByIdsAsync(distinctRequestedIds, cancellationToken);
        if (lots.Count == 0)
            return new Dictionary<Guid, LotJourneyDto>();

        var lotsDict = lots.ToDictionary(l => l.Id);
        var existingLotIds = lotsDict.Keys.ToList();

        // 2. Single DB call: fetch ONLY movements for the existing requested lots
        var allMovements = await _movementRepository.GetMovementsForLotsAsync(existingLotIds, cancellationToken);
        var movementsLookup = allMovements.ToLookup(m => m.ProductionLotId);

        // 3. Single DB call: fetch steps for active tenant
        var steps = (await _stepRepository.GetAllAsync(cancellationToken)).ToDictionary(s => s.Id);
        var terminatingStepIds = steps.Values.Where(s => s.IsTerminating).Select(s => s.Id).ToHashSet() as IReadOnlySet<Guid>;

        // Build all journeys in-memory — bounded allocations
        var result = new Dictionary<Guid, LotJourneyDto>(lots.Count);

        foreach (var lotId in existingLotIds)
        {
            var lot = lotsDict[lotId];
            var movements = movementsLookup[lotId].ToList();

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

            var snapshot = _replayEngine.ReplayLedger(lotId, movements, terminatingStepIds);
            var activeWips = snapshot.StepBalances.Select(sb =>
            {
                steps.TryGetValue(sb.Key, out var step);
                return new StepWipDto(sb.Key, step?.Code ?? "UNKNOWN", step?.Name ?? "Unknown Step", sb.Value);
            }).ToList();

            result[lotId] = new LotJourneyDto(
                lot.Id,
                lot.LotNumber.Value,
                lot.Status.ToString(),
                lot.CreatedAtUtc,
                timeline,
                activeWips,
                lot.ClientId,
                lot.ClientName);
        }

        return result;
    }
}
