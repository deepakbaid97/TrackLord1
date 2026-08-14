using System;
using System.Collections.Generic;
using System.Linq;
using TrackLord.Application.Queries.Dashboard.Strategies;
using TrackLord.Domain.Aggregates.ClientAggregate;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Services;
using TrackLord.Domain.ValueObjects;
using Xunit;

namespace TrackLord.Tests;

public class TenantSettingsAndStrategyTests
{
    [Fact]
    public void Tenant_UpdateSettings_PersistsValidSettingsJson()
    {
        var tenant = new Tenant(Guid.NewGuid(), "Acme Apparel", "ACME");
        Assert.Equal("{}", tenant.SettingsJson);

        var customJson = "{\"clientMode\":\"lot\",\"featureOverrides\":{\"pages.clients\":false}}";
        tenant.UpdateSettings(customJson);

        Assert.Equal(customJson, tenant.SettingsJson);
    }

    [Fact]
    public void LotLevelClientAggregationStrategy_AttributesWipDirectlyToLotClient()
    {
        var clientA = new Client(Guid.NewGuid(), "Client Alpha", "ALPHA", "Customer");
        var clientB = new Client(Guid.NewGuid(), "Client Beta", "BETA", "Customer");
        var clientsDict = new Dictionary<Guid, Client>
        {
            { clientA.Id, clientA },
            { clientB.Id, clientB }
        };

        var lot1 = ProductionLot.Create(Guid.NewGuid(), ProductionLotNumber.Create("LOT-001"), Guid.NewGuid(), new Quantity(100, UnitType.Pieces), Guid.NewGuid(), clientId: clientA.Id, clientName: clientA.Name);
        var lot2 = ProductionLot.Create(Guid.NewGuid(), ProductionLotNumber.Create("LOT-002"), Guid.NewGuid(), new Quantity(200, UnitType.Pieces), Guid.NewGuid(), clientId: clientA.Id, clientName: clientA.Name);
        var lot3 = ProductionLot.Create(Guid.NewGuid(), ProductionLotNumber.Create("LOT-003"), Guid.NewGuid(), new Quantity(50, UnitType.Pieces), Guid.NewGuid(), clientId: clientB.Id, clientName: clientB.Name);

        var activeLots = new List<ProductionLot> { lot1, lot2, lot3 };

        var lotSnapshots = new Dictionary<Guid, LotLedgerSnapshot>
        {
            { lot1.Id, new LotLedgerSnapshot(lot1.Id, new Dictionary<Guid, decimal>(), 100m, 0m, DateTime.UtcNow, 1) },
            { lot2.Id, new LotLedgerSnapshot(lot2.Id, new Dictionary<Guid, decimal>(), 180m, 20m, DateTime.UtcNow, 2) },
            { lot3.Id, new LotLedgerSnapshot(lot3.Id, new Dictionary<Guid, decimal>(), 50m, 0m, DateTime.UtcNow, 1) }
        };

        var emptyMovementsLookup = new List<ManufacturingMovement>().ToLookup(m => m.ProductionLotId);

        var strategy = new LotLevelClientAggregationStrategy();
        var result = strategy.AggregateClientSummaries(activeLots, emptyMovementsLookup, clientsDict, lotSnapshots);

        Assert.Equal(2, result.Count);

        var summaryA = result.First(r => r.ClientId == clientA.Id);
        Assert.Equal(2, summaryA.TotalLotsAssociated);
        Assert.Equal(280m, summaryA.TotalQuantityMoved); // 100 + 180

        var summaryB = result.First(r => r.ClientId == clientB.Id);
        Assert.Equal(1, summaryB.TotalLotsAssociated);
        Assert.Equal(50m, summaryB.TotalQuantityMoved);
    }

    [Fact]
    public void StepLevelClientAggregationStrategy_AttributesWipFromMovementLedger()
    {
        var clientX = new Client(Guid.NewGuid(), "Stitching Vendor X", "VENDX", "Subcontractor");
        var clientsDict = new Dictionary<Guid, Client>
        {
            { clientX.Id, clientX }
        };

        var lot = ProductionLot.Create(Guid.NewGuid(), ProductionLotNumber.Create("LOT-100"), Guid.NewGuid(), new Quantity(100, UnitType.Pieces), Guid.NewGuid());
        var activeLots = new List<ProductionLot> { lot };

        var m1 = ManufacturingMovement.Create(Guid.NewGuid(), lot.Id, null, Guid.NewGuid(), 100m, MovementType.StepTransfer, DateTime.UtcNow, clientId: clientX.Id);
        var m2Reversed = ManufacturingMovement.Create(Guid.NewGuid(), lot.Id, null, Guid.NewGuid(), 30m, MovementType.StepTransfer, DateTime.UtcNow, clientId: clientX.Id);
        m2Reversed.CreateReversal(Guid.NewGuid(), "test reason", "admin");

        var movementsLookup = new List<ManufacturingMovement> { m1, m2Reversed }.ToLookup(m => m.ProductionLotId);

        var strategy = new StepLevelClientAggregationStrategy();
        var result = strategy.AggregateClientSummaries(activeLots, movementsLookup, clientsDict, new Dictionary<Guid, LotLedgerSnapshot>());

        Assert.Single(result);
        Assert.Equal(100m, result[0].TotalQuantityMoved); // ignores reversed 30m
        Assert.Equal(1, result[0].TotalLotsAssociated);
    }

    [Fact]
    public void NullClientAggregationStrategy_ReturnsEmptyList()
    {
        var strategy = new NullClientAggregationStrategy();
        var result = strategy.AggregateClientSummaries(
            new List<ProductionLot>(),
            new List<ManufacturingMovement>().ToLookup(m => m.ProductionLotId),
            new Dictionary<Guid, Client>(),
            new Dictionary<Guid, LotLedgerSnapshot>());

        Assert.Empty(result);
    }
}
