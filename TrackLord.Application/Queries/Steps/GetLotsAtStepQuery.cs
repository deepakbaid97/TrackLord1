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

public record StepLotItemDto(
    Guid LotId,
    string LotNumber,
    decimal QuantityAtStep,
    decimal InitialQuantity,
    string Status,
    DateTime StepCreatedAt,
    DateTime StepCreatedAtUtc,
    Guid? MovementId,
    string? ClientName,
    Guid? ClientId,
    string? Remarks);

public record StepLotsResponseDto(
    Guid StepId,
    string StepCode,
    string StepName,
    DateTime? EvaluatedAsOfDate,
    DateTime? EvaluatedFromDate,
    DateTime? EvaluatedToDate,
    decimal TotalQuantity,
    int TotalLots,
    IReadOnlyList<StepLotItemDto> Lots);

public record GetLotsAtStepQuery(
    Guid StepId,
    DateTime? AsOfDate = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Search = null) : IRequest<StepLotsResponseDto>;

public class GetLotsAtStepQueryHandler : IRequestHandler<GetLotsAtStepQuery, StepLotsResponseDto>
{
    private readonly IManufacturingStepRepository _stepRepository;
    private readonly IProductionLotRepository _lotRepository;
    private readonly IManufacturingMovementRepository _movementRepository;
    private readonly ILedgerReplayEngine _replayEngine;

    public GetLotsAtStepQueryHandler(
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

    public async Task<StepLotsResponseDto> Handle(GetLotsAtStepQuery request, CancellationToken cancellationToken)
    {
        var step = await _stepRepository.GetByIdAsync(request.StepId, cancellationToken);
        if (step is null)
        {
            throw new DomainException($"Manufacturing step with ID '{request.StepId}' not found.");
        }

        var allSteps = await _stepRepository.GetAllAsync(cancellationToken);
        var terminatingStepIds = allSteps.Where(s => s.IsTerminating).Select(s => s.Id).ToHashSet() as IReadOnlySet<Guid>;

        DateTime? maxDate = request.AsOfDate ?? request.ToDate;
        if (maxDate.HasValue && maxDate.Value.Kind != DateTimeKind.Utc)
        {
            maxDate = DateTime.SpecifyKind(maxDate.Value, DateTimeKind.Utc);
        }

        DateTime? minDate = request.FromDate;
        if (minDate.HasValue && minDate.Value.Kind != DateTimeKind.Utc)
        {
            minDate = DateTime.SpecifyKind(minDate.Value, DateTimeKind.Utc);
        }

        // 1. Indexed seek: Find only production lots that had an incoming movement to this step up to maxDate
        var candidateLotIds = await _movementRepository.GetCandidateLotIdsForStepAsync(
            request.StepId,
            maxDate,
            cancellationToken);

        if (candidateLotIds.Count == 0)
        {
            return new StepLotsResponseDto(
                step.Id,
                step.Code,
                step.Name,
                request.AsOfDate,
                request.FromDate,
                request.ToDate,
                0m,
                0,
                Array.Empty<StepLotItemDto>());
        }

        // 2. Fetch lots and their movements
        var lots = await _lotRepository.GetByIdsAsync(candidateLotIds, cancellationToken);
        var lotsDict = lots.ToDictionary(l => l.Id);
        var allMovements = await _movementRepository.GetMovementsForLotsAsync(candidateLotIds, cancellationToken);
        var movementsLookup = allMovements.ToLookup(m => m.ProductionLotId);

        var resultItems = new List<StepLotItemDto>();

        foreach (var lotId in candidateLotIds)
        {
            if (!lotsDict.TryGetValue(lotId, out var lot)) continue;

            var movements = movementsLookup[lotId].ToList();
            if (movements.Count == 0) continue;

            bool isMatching = false;
            decimal qtyAtStep = 0m;

            if (request.AsOfDate.HasValue)
            {
                // Point in time replay as of AsOfDate
                var snapshot = _replayEngine.ReplayLedger(lotId, movements, terminatingStepIds, asOfDate: request.AsOfDate.Value);
                if (snapshot.StepBalances.TryGetValue(request.StepId, out var qty) && qty > 0)
                {
                    isMatching = true;
                    qtyAtStep = qty;
                }
            }
            else if (request.FromDate.HasValue || request.ToDate.HasValue)
            {
                // Date period [minDate, maxDate]
                var effectiveStart = minDate ?? DateTime.MinValue;
                var effectiveEnd = maxDate ?? DateTime.UtcNow;

                // Did the lot have balance at effectiveStart?
                var snapshotStart = _replayEngine.ReplayLedger(lotId, movements, terminatingStepIds, asOfDate: effectiveStart);
                var hadBalanceAtStart = snapshotStart.StepBalances.TryGetValue(request.StepId, out var qtyStart) && qtyStart > 0;

                // Did the lot receive any movements into this step during the period?
                var hasIncomingMovementInPeriod = movements.Any(m =>
                    m.ToStepId == request.StepId &&
                    !m.IsReversed &&
                    m.MovementDate >= effectiveStart &&
                    m.MovementDate <= effectiveEnd);

                if (hadBalanceAtStart || hasIncomingMovementInPeriod)
                {
                    isMatching = true;
                    // Balance as of effectiveEnd (or incoming movement qty if it was consumed/moved later)
                    var snapshotEnd = _replayEngine.ReplayLedger(lotId, movements, terminatingStepIds, asOfDate: effectiveEnd);
                    if (snapshotEnd.StepBalances.TryGetValue(request.StepId, out var qtyEnd) && qtyEnd > 0)
                    {
                        qtyAtStep = qtyEnd;
                    }
                    else
                    {
                        var periodIncoming = movements
                            .Where(m => m.ToStepId == request.StepId && !m.IsReversed && m.MovementDate <= effectiveEnd)
                            .OrderByDescending(m => m.MovementDate)
                            .FirstOrDefault();
                        qtyAtStep = periodIncoming?.Quantity ?? (hadBalanceAtStart ? qtyStart : 0m);
                    }
                }
            }
            else
            {
                // Current live balance
                var snapshot = _replayEngine.ReplayLedger(lotId, movements, terminatingStepIds);
                if (snapshot.StepBalances.TryGetValue(request.StepId, out var qty) && qty > 0)
                {
                    isMatching = true;
                    qtyAtStep = qty;
                }
            }

            if (!isMatching) continue;

            // Find the movement that created/entered this step (the latest non-reversed movement to this step up to maxDate)
            var stepEntryMovement = movements
                .Where(m => m.ToStepId == request.StepId && !m.IsReversed && (!maxDate.HasValue || m.MovementDate <= maxDate.Value))
                .OrderByDescending(m => m.MovementDate)
                .ThenByDescending(m => m.CreatedAtUtc)
                .FirstOrDefault();

            var stepCreatedAt = stepEntryMovement?.MovementDate ?? lot.CreatedAtUtc;
            var stepCreatedAtUtc = stepEntryMovement?.CreatedAtUtc ?? lot.CreatedAtUtc;
            var movementId = stepEntryMovement?.Id;
            var remarks = stepEntryMovement?.Remarks ?? lot.Remarks;

            // Optional search filter
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search.Trim();
                var lotMatches = lot.LotNumber.Value.Contains(s, StringComparison.OrdinalIgnoreCase);
                var clientMatches = lot.ClientName?.Contains(s, StringComparison.OrdinalIgnoreCase) == true;
                if (!lotMatches && !clientMatches)
                {
                    continue;
                }
            }

            resultItems.Add(new StepLotItemDto(
                lot.Id,
                lot.LotNumber.Value,
                qtyAtStep,
                lot.InitialQuantity.Value,
                lot.Status.ToString(),
                stepCreatedAt,
                stepCreatedAtUtc,
                movementId,
                lot.ClientName,
                lot.ClientId,
                remarks));
        }

        // Sort by StepCreatedAt descending (newest entries first)
        var sortedResults = resultItems
            .OrderByDescending(x => x.StepCreatedAt)
            .ThenBy(x => x.LotNumber)
            .ToList();

        var totalQty = sortedResults.Sum(x => x.QuantityAtStep);

        return new StepLotsResponseDto(
            step.Id,
            step.Code,
            step.Name,
            request.AsOfDate,
            request.FromDate,
            request.ToDate,
            totalQty,
            sortedResults.Count,
            sortedResults);
    }
}
