using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TrackLord.Domain.Aggregates.ClientAggregate;
using TrackLord.Domain.Aggregates.MaterialLotAggregate;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Aggregates.UserAggregate;
using TrackLord.Domain.Common;
using TrackLord.Domain.Enums;
using TrackLord.Domain.ValueObjects;
using TrackLord.Infrastructure.Persistence;
using TrackLord.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TrackLord.Tests;

public class MockTenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }

    public MockTenantContext(Guid tenantId)
    {
        TenantId = tenantId;
    }

    public void SetTenantId(Guid tenantId)
    {
        TenantId = tenantId;
    }
}

public class TenantIsolationTests
{
    [Fact]
    public async Task EntitiesCreatedInTenantA_ShouldNotBeVisibleInTenantB()
    {
        // Arrange
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<TrackLordDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var contextA = new MockTenantContext(tenantAId);
        var contextB = new MockTenantContext(tenantBId);

        // 1. Add records under Tenant A
        using (var dbA = new TrackLordDbContext(options, contextA))
        {
            var tenantA = new Tenant(tenantAId, "Alpha Textiles", "ALPHA");
            dbA.Tenants.Add(tenantA);

            var step = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, Guid.NewGuid(), false, tenantAId);
            var lot = ProductionLot.Create(Guid.NewGuid(), ProductionLotNumber.Create("LOT-ALPHA-101"), Guid.NewGuid(), new Quantity(100, UnitType.Pieces), step.Id, tenantId: tenantAId);
            var user = new User(Guid.NewGuid(), "+919999911111", "Alpha Admin", "Admin", tenantId: tenantAId);
            var client = new Client(Guid.NewGuid(), "Alpha Buyer", "+919999922222", "Wholesaler", tenantId: tenantAId);

            dbA.ManufacturingSteps.Add(step);
            dbA.ProductionLots.Add(lot);
            dbA.Users.Add(user);
            dbA.Clients.Add(client);

            await dbA.SaveChangesAsync();
        }

        // 2. Add records under Tenant B
        using (var dbB = new TrackLordDbContext(options, contextB))
        {
            var tenantB = new Tenant(tenantBId, "Beta Garments", "BETA");
            dbB.Tenants.Add(tenantB);

            var stepB = new ManufacturingStep(Guid.NewGuid(), "SEW", "Sewing", 1, Guid.NewGuid(), false, tenantBId);
            var lotB = ProductionLot.Create(Guid.NewGuid(), ProductionLotNumber.Create("LOT-BETA-202"), Guid.NewGuid(), new Quantity(200, UnitType.Pieces), stepB.Id, tenantId: tenantBId);

            dbB.ManufacturingSteps.Add(stepB);
            dbB.ProductionLots.Add(lotB);

            await dbB.SaveChangesAsync();
        }

        // Assert: Querying with Tenant A Context only returns Tenant A data
        using (var queryDbA = new TrackLordDbContext(options, contextA))
        {
            var lotRepo = new ProductionLotRepository(queryDbA);
            var stepRepo = new ManufacturingStepRepository(queryDbA);
            var userRepo = new UserRepository(queryDbA);

            var lots = await lotRepo.GetAllAsync();
            var steps = await stepRepo.GetAllAsync();
            var users = await userRepo.GetAllAsync();

            lots.Should().HaveCount(1);
            lots[0].LotNumber.Value.Should().Be("LOT-ALPHA-101");

            steps.Should().HaveCount(1);
            steps[0].Code.Should().Be("CUT");

            users.Should().HaveCount(1);
            users[0].Name.Should().Be("Alpha Admin");
        }

        // Assert: Querying with Tenant B Context only returns Tenant B data
        using (var queryDbB = new TrackLordDbContext(options, contextB))
        {
            var lotRepoB = new ProductionLotRepository(queryDbB);
            var stepRepoB = new ManufacturingStepRepository(queryDbB);

            var lotsB = await lotRepoB.GetAllAsync();
            var stepsB = await stepRepoB.GetAllAsync();

            lotsB.Should().HaveCount(1);
            lotsB[0].LotNumber.Value.Should().Be("LOT-BETA-202");

            stepsB.Should().HaveCount(1);
            stepsB[0].Code.Should().Be("SEW");
        }
    }
}
