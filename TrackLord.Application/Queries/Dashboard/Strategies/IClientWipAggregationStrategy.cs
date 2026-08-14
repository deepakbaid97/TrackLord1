using System;
using System.Collections.Generic;
using System.Linq;
using TrackLord.Application.Queries;
using TrackLord.Domain.Aggregates.ClientAggregate;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Services;

namespace TrackLord.Application.Queries.Dashboard.Strategies;

public interface IClientWipAggregationStrategy
{
    IReadOnlyList<DashboardClientSummaryDto> AggregateClientSummaries(
        IReadOnlyList<ProductionLot> activeLots,
        ILookup<Guid, ManufacturingMovement> movementsLookup,
        IReadOnlyDictionary<Guid, Client> clientsDict,
        IReadOnlyDictionary<Guid, LotLedgerSnapshot> lotSnapshots);
}
