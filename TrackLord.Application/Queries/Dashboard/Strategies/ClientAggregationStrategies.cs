using System;
using System.Collections.Generic;
using System.Linq;
using TrackLord.Domain.Aggregates.ClientAggregate;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Services;

namespace TrackLord.Application.Queries.Dashboard.Strategies;

/// <summary>
/// Aggregates client summaries when a production lot belongs to a single client (LotLevel mode).
/// WIP of each active lot is attributed directly to that lot's client.
/// </summary>
public class LotLevelClientAggregationStrategy : IClientWipAggregationStrategy
{
    public IReadOnlyList<DashboardClientSummaryDto> AggregateClientSummaries(
        IReadOnlyList<ProductionLot> activeLots,
        ILookup<Guid, ManufacturingMovement> movementsLookup,
        IReadOnlyDictionary<Guid, Client> clientsDict,
        IReadOnlyDictionary<Guid, LotLedgerSnapshot> lotSnapshots)
    {
        var clientAggregation = new Dictionary<Guid, (int lotCount, decimal totalWip)>();

        foreach (var lot in activeLots)
        {
            if (!lot.ClientId.HasValue) continue;

            var cid = lot.ClientId.Value;
            var lotActiveWip = lotSnapshots.TryGetValue(lot.Id, out var snapshot)
                ? snapshot.TotalActiveQuantity
                : 0m;

            if (!clientAggregation.ContainsKey(cid))
            {
                clientAggregation[cid] = (0, 0m);
            }

            var current = clientAggregation[cid];
            clientAggregation[cid] = (current.lotCount + 1, current.totalWip + lotActiveWip);
        }

        return clientAggregation
            .Select(kvp =>
            {
                clientsDict.TryGetValue(kvp.Key, out var client);
                return new DashboardClientSummaryDto(
                    kvp.Key,
                    client?.Name ?? "Unknown",
                    client?.ClientType ?? "Customer",
                    kvp.Value.lotCount,
                    kvp.Value.totalWip);
            })
            .OrderByDescending(c => c.TotalQuantityMoved)
            .ToList();
    }
}

/// <summary>
/// Aggregates client summaries when clients/subcontractors are tagged on individual step movements (StepLevel mode / Job-work).
/// </summary>
public class StepLevelClientAggregationStrategy : IClientWipAggregationStrategy
{
    public IReadOnlyList<DashboardClientSummaryDto> AggregateClientSummaries(
        IReadOnlyList<ProductionLot> activeLots,
        ILookup<Guid, ManufacturingMovement> movementsLookup,
        IReadOnlyDictionary<Guid, Client> clientsDict,
        IReadOnlyDictionary<Guid, LotLedgerSnapshot> lotSnapshots)
    {
        var clientAggregation = new Dictionary<Guid, (HashSet<Guid> lots, decimal totalQty)>();

        foreach (var lot in activeLots)
        {
            var movements = movementsLookup[lot.Id];
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

        return clientAggregation
            .Select(kvp =>
            {
                clientsDict.TryGetValue(kvp.Key, out var client);
                return new DashboardClientSummaryDto(
                    kvp.Key,
                    client?.Name ?? "Unknown",
                    client?.ClientType ?? "Subcontractor",
                    kvp.Value.lots.Count,
                    kvp.Value.totalQty);
            })
            .OrderByDescending(c => c.TotalQuantityMoved)
            .ToList();
    }
}

/// <summary>
/// Null strategy when client tracking is disabled.
/// </summary>
public class NullClientAggregationStrategy : IClientWipAggregationStrategy
{
    public IReadOnlyList<DashboardClientSummaryDto> AggregateClientSummaries(
        IReadOnlyList<ProductionLot> activeLots,
        ILookup<Guid, ManufacturingMovement> movementsLookup,
        IReadOnlyDictionary<Guid, Client> clientsDict,
        IReadOnlyDictionary<Guid, LotLedgerSnapshot> lotSnapshots)
    {
        return Array.Empty<DashboardClientSummaryDto>();
    }
}

/// <summary>
/// Factory to provide the appropriate aggregation strategy for the tenant.
/// </summary>
public static class ClientAggregationStrategyFactory
{
    public static IClientWipAggregationStrategy Create(ClientTrackingMode mode) => mode switch
    {
        ClientTrackingMode.LotLevel => new LotLevelClientAggregationStrategy(),
        ClientTrackingMode.StepLevel => new StepLevelClientAggregationStrategy(),
        ClientTrackingMode.None => new NullClientAggregationStrategy(),
        _ => new StepLevelClientAggregationStrategy()
    };
}
