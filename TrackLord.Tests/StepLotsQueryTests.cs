using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using TrackLord.Application.Commands;
using TrackLord.Application.Queries;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Enums;
using TrackLord.Infrastructure.Persistence.Repositories;
using TrackLord.Infrastructure.Services;
using Xunit;

namespace TrackLord.Tests;

public class StepLotsQueryTests
{
    private readonly LedgerReplayEngine _replayEngine = new();

    [Fact]
    public async Task GetLotsAtStep_Current_ReturnsLotsCurrentlyAtStepWithStepMovementDate()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var factoryId = Guid.NewGuid();
        var cutting = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, factoryId);
        var stitching = new ManufacturingStep(Guid.NewGuid(), "STITCH", "Stitching", 2, factoryId);

        await stepRepo.AddAsync(cutting);
        await stepRepo.AddAsync(stitching);

        var dateAug1 = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var createHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lotId = await createHandler.Handle(
            new CreateProductionLotCommand("LOT-STEP-01", Guid.NewGuid(), 100m, UnitType.Pieces, cutting.Id, dateAug1),
            default);

        var dateAug5 = new DateTime(2026, 8, 5, 14, 30, 0, DateTimeKind.Utc);
        var moveHandler = new MoveLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        await moveHandler.Handle(new MoveLotCommand(lotId, cutting.Id, stitching.Id, 70m, dateAug5), default);

        var queryHandler = new GetLotsAtStepQueryHandler(stepRepo, lotRepo, movementRepo, _replayEngine);

        // Act 1: Query Cutting currently
        var cuttingResult = await queryHandler.Handle(new GetLotsAtStepQuery(cutting.Id), default);
        cuttingResult.Lots.Should().HaveCount(1);
        cuttingResult.Lots[0].LotNumber.Should().Be("LOT-STEP-01");
        cuttingResult.Lots[0].QuantityAtStep.Should().Be(30m);
        // In Cutting, entry movement was lot creation on Aug 1
        cuttingResult.Lots[0].StepCreatedAt.Should().Be(dateAug1);

        // Act 2: Query Stitching currently
        var stitchingResult = await queryHandler.Handle(new GetLotsAtStepQuery(stitching.Id), default);
        stitchingResult.Lots.Should().HaveCount(1);
        stitchingResult.Lots[0].LotNumber.Should().Be("LOT-STEP-01");
        stitchingResult.Lots[0].QuantityAtStep.Should().Be(70m);
        // In Stitching, entry movement was the transfer on Aug 5!
        stitchingResult.Lots[0].StepCreatedAt.Should().Be(dateAug5);
    }

    [Fact]
    public async Task GetLotsAtStep_HistoricalAsOfDate_ReturnsBalancesAtThatPointInTime()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var factoryId = Guid.NewGuid();
        var cutting = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, factoryId);
        var stitching = new ManufacturingStep(Guid.NewGuid(), "STITCH", "Stitching", 2, factoryId);

        await stepRepo.AddAsync(cutting);
        await stepRepo.AddAsync(stitching);

        var dateAug1 = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var createHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lotId = await createHandler.Handle(
            new CreateProductionLotCommand("LOT-HIST-01", Guid.NewGuid(), 100m, UnitType.Pieces, cutting.Id, dateAug1),
            default);

        var dateAug5 = new DateTime(2026, 8, 5, 14, 30, 0, DateTimeKind.Utc);
        var moveHandler = new MoveLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        await moveHandler.Handle(new MoveLotCommand(lotId, cutting.Id, stitching.Id, 100m, dateAug5), default);

        var queryHandler = new GetLotsAtStepQueryHandler(stepRepo, lotRepo, movementRepo, _replayEngine);

        // As of Aug 3 (before movement): Lot was 100% in Cutting, 0 in Stitching
        var dateAug3 = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);

        var cuttingAsOfAug3 = await queryHandler.Handle(new GetLotsAtStepQuery(cutting.Id, AsOfDate: dateAug3), default);
        cuttingAsOfAug3.Lots.Should().HaveCount(1);
        cuttingAsOfAug3.Lots[0].QuantityAtStep.Should().Be(100m);
        cuttingAsOfAug3.Lots[0].StepCreatedAt.Should().Be(dateAug1);

        var stitchingAsOfAug3 = await queryHandler.Handle(new GetLotsAtStepQuery(stitching.Id, AsOfDate: dateAug3), default);
        stitchingAsOfAug3.Lots.Should().BeEmpty();

        // As of Aug 6 (after movement): Lot was in Stitching
        var dateAug6 = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc);
        var stitchingAsOfAug6 = await queryHandler.Handle(new GetLotsAtStepQuery(stitching.Id, AsOfDate: dateAug6), default);
        stitchingAsOfAug6.Lots.Should().HaveCount(1);
        stitchingAsOfAug6.Lots[0].QuantityAtStep.Should().Be(100m);
        stitchingAsOfAug6.Lots[0].StepCreatedAt.Should().Be(dateAug5);
    }

    [Fact]
    public async Task GetLotsAtStep_DateRange_ReturnsLotsPresentDuringPeriod()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var factoryId = Guid.NewGuid();
        var cutting = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, factoryId);
        var stitching = new ManufacturingStep(Guid.NewGuid(), "STITCH", "Stitching", 2, factoryId);

        await stepRepo.AddAsync(cutting);
        await stepRepo.AddAsync(stitching);

        // Lot 1: Aug 1 created at Cutting. Aug 10 moved to Stitching.
        var dateAug1 = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var createHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lot1 = await createHandler.Handle(
            new CreateProductionLotCommand("LOT-PERIOD-01", Guid.NewGuid(), 50m, UnitType.Pieces, cutting.Id, dateAug1),
            default);

        var dateAug10 = new DateTime(2026, 8, 10, 10, 0, 0, DateTimeKind.Utc);
        var moveHandler = new MoveLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        await moveHandler.Handle(new MoveLotCommand(lot1, cutting.Id, stitching.Id, 50m, dateAug10), default);

        var queryHandler = new GetLotsAtStepQueryHandler(stepRepo, lotRepo, movementRepo, _replayEngine);

        // Range: Aug 2 to Aug 5 (Lot 1 was in Cutting)
        var rangeResult = await queryHandler.Handle(new GetLotsAtStepQuery(
            cutting.Id,
            FromDate: new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
            ToDate: new DateTime(2026, 8, 5, 23, 59, 59, DateTimeKind.Utc)),
            default);

        rangeResult.Lots.Should().HaveCount(1);
        rangeResult.Lots[0].LotNumber.Should().Be("LOT-PERIOD-01");
        rangeResult.Lots[0].StepCreatedAt.Should().Be(dateAug1);
    }
}
