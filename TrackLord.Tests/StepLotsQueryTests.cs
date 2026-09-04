using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using TrackLord.Application.Commands;
using TrackLord.Application.Queries;
using TrackLord.Domain.Aggregates.MovementAggregate;
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
    public async Task GetLotsAtStep_Period_OnlyIncludesLotsEnteringWithinDateRangeAndExcludesOlderLots()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var factoryId = Guid.NewGuid();
        var cutting = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, factoryId);
        await stepRepo.AddAsync(cutting);

        var createHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);

        // Lot 1: Entered Cutting on Aug 1 (before filter start date)
        var dateAug1 = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        await createHandler.Handle(
            new CreateProductionLotCommand("LOT-OLD-01", Guid.NewGuid(), 50m, UnitType.Pieces, cutting.Id, dateAug1),
            default);

        // Lot 2: Entered Cutting on Aug 3 (INSIDE filter range Aug 2 - Aug 5)
        var dateAug3 = new DateTime(2026, 8, 3, 14, 0, 0, DateTimeKind.Utc);
        await createHandler.Handle(
            new CreateProductionLotCommand("LOT-INSIDE-02", Guid.NewGuid(), 30m, UnitType.Pieces, cutting.Id, dateAug3),
            default);

        // Lot 3: Entered Cutting on Aug 7 (AFTER filter end date)
        var dateAug7 = new DateTime(2026, 8, 7, 9, 0, 0, DateTimeKind.Utc);
        await createHandler.Handle(
            new CreateProductionLotCommand("LOT-AFTER-03", Guid.NewGuid(), 20m, UnitType.Pieces, cutting.Id, dateAug7),
            default);

        var queryHandler = new GetLotsAtStepQueryHandler(stepRepo, lotRepo, movementRepo, _replayEngine);

        var fromDate = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc);
        var toDate = new DateTime(2026, 8, 5, 23, 59, 59, DateTimeKind.Utc);

        // Query Period: Aug 2 to Aug 5
        var periodResult = await queryHandler.Handle(new GetLotsAtStepQuery(
            cutting.Id,
            FromDate: fromDate,
            ToDate: toDate),
            default);

        // Assert: ONLY Lot 2 must be returned. Lot 1 (Aug 1) and Lot 3 (Aug 7) MUST be excluded.
        periodResult.Lots.Should().HaveCount(1);
        periodResult.Lots[0].LotNumber.Should().Be("LOT-INSIDE-02");
        periodResult.Lots[0].StepCreatedAt.Should().Be(dateAug3);
        periodResult.Lots.All(l => l.StepCreatedAt >= fromDate && l.StepCreatedAt <= toDate).Should().BeTrue();
    }

    [Fact]
    public async Task GetLotsAtStep_Period_IncludesLotTransferredIntoStepDuringPeriod()
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

        // Lot created in Cutting on Aug 1
        var dateAug1 = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var createHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lotId = await createHandler.Handle(
            new CreateProductionLotCommand("LOT-TRANSFER-01", Guid.NewGuid(), 100m, UnitType.Pieces, cutting.Id, dateAug1),
            default);

        // Transferred to Stitching on Aug 4 (inside Aug 2 - Aug 5)
        var dateAug4 = new DateTime(2026, 8, 4, 15, 0, 0, DateTimeKind.Utc);
        var moveHandler = new MoveLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        await moveHandler.Handle(new MoveLotCommand(lotId, cutting.Id, stitching.Id, 100m, dateAug4), default);

        var queryHandler = new GetLotsAtStepQueryHandler(stepRepo, lotRepo, movementRepo, _replayEngine);

        var fromDate = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc);
        var toDate = new DateTime(2026, 8, 5, 23, 59, 59, DateTimeKind.Utc);

        // Query Stitching for Period Aug 2 - Aug 5
        var stitchingPeriod = await queryHandler.Handle(new GetLotsAtStepQuery(
            stitching.Id,
            FromDate: fromDate,
            ToDate: toDate),
            default);

        stitchingPeriod.Lots.Should().HaveCount(1);
        stitchingPeriod.Lots[0].LotNumber.Should().Be("LOT-TRANSFER-01");
        stitchingPeriod.Lots[0].StepCreatedAt.Should().Be(dateAug4);
        stitchingPeriod.Lots.All(l => l.StepCreatedAt >= fromDate && l.StepCreatedAt <= toDate).Should().BeTrue();

        // In Cutting for Period Aug 2 - Aug 5, this lot had NO entry movements (it was created on Aug 1)
        var cuttingPeriod = await queryHandler.Handle(new GetLotsAtStepQuery(
            cutting.Id,
            FromDate: fromDate,
            ToDate: toDate),
            default);

        cuttingPeriod.Lots.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCandidateLotIdsForStepAsync_FiltersByMinAndMaxMovementDate()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var movementRepo = new ManufacturingMovementRepository(db);

        var stepId = Guid.NewGuid();
        var lot1 = Guid.NewGuid();
        var lot2 = Guid.NewGuid();
        var lot3 = Guid.NewGuid();

        // Movement 1 on Aug 1
        await movementRepo.AddAsync(ManufacturingMovement.Create(
            Guid.NewGuid(), lot1, null, stepId, 10m, MovementType.InitialProduction,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)));

        // Movement 2 on Aug 3
        await movementRepo.AddAsync(ManufacturingMovement.Create(
            Guid.NewGuid(), lot2, null, stepId, 10m, MovementType.InitialProduction,
            new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc)));

        // Movement 3 on Aug 7
        await movementRepo.AddAsync(ManufacturingMovement.Create(
            Guid.NewGuid(), lot3, null, stepId, 10m, MovementType.InitialProduction,
            new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)));

        // Filter Aug 2 to Aug 5
        var candidates = await movementRepo.GetCandidateLotIdsForStepAsync(
            stepId,
            maxMovementDate: new DateTime(2026, 8, 5, 23, 59, 59, DateTimeKind.Utc),
            minMovementDate: new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc));

        candidates.Should().ContainSingle().Which.Should().Be(lot2);
    }
}
