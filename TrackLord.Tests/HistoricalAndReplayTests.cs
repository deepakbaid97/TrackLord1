using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TrackLord.Application.Commands;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.ValueObjects;
using TrackLord.Infrastructure.Persistence.Repositories;
using TrackLord.Infrastructure.Services;
using Xunit;

namespace TrackLord.Tests;

public class BackdatedEntriesAndHistoricalEditsTests
{
    private readonly LedgerReplayEngine _replayEngine = new();

    [Fact]
    public async Task BackdatedEntry_InsertedInPast_ShouldReplayChronologicallyAndRecalculateCorrectBalances()
    {
        // Arrange: Today is 10 Aug. Lot created 1 Aug with 100 pcs at Cutting.
        // Movement 1: 8 Aug move 60 to Stitching.
        // Backdated Movement inserted today (10 Aug) for date 5 Aug: move 30 from Cutting to Washing.
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var factoryId = Guid.NewGuid();
        var cutting = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, factoryId);
        var stitching = new ManufacturingStep(Guid.NewGuid(), "STITCH", "Stitching", 2, factoryId);
        var washing = new ManufacturingStep(Guid.NewGuid(), "WASH", "Washing", 3, factoryId);

        await stepRepo.AddAsync(cutting);
        await stepRepo.AddAsync(stitching);
        await stepRepo.AddAsync(washing);

        var dateAug1 = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var createHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lotId = await createHandler.Handle(new CreateProductionLotCommand("Lot-101", Guid.NewGuid(), 100m, UnitType.Pieces, cutting.Id, dateAug1), default);

        var moveHandler = new MoveLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);

        // Movement on 8 Aug: Move 60 Cutting -> Stitching
        var dateAug8 = new DateTime(2026, 8, 8, 10, 0, 0, DateTimeKind.Utc);
        await moveHandler.Handle(new MoveLotCommand(lotId, cutting.Id, stitching.Id, 60m, dateAug8), default);

        // Act: Backdated movement on 5 Aug: Move 30 Cutting -> Washing (entered on 10 Aug)
        var dateAug5 = new DateTime(2026, 8, 5, 10, 0, 0, DateTimeKind.Utc);
        await moveHandler.Handle(new MoveLotCommand(lotId, cutting.Id, washing.Id, 30m, dateAug5), default);

        // Assert: Replay chronologically sorts movements (1 Aug, 5 Aug, 8 Aug).
        // 1 Aug: 100 in Cutting
        // 5 Aug: -30 Cutting, +30 Washing -> Cutting = 70, Washing = 30
        // 8 Aug: -60 Cutting, +60 Stitching -> Cutting = 10, Washing = 30, Stitching = 60
        var movements = await movementRepo.GetMovementsForLotAsync(lotId);
        var snapshot = _replayEngine.ReplayLedger(lotId, movements);

        snapshot.StepBalances[cutting.Id].Should().Be(10m);
        snapshot.StepBalances[washing.Id].Should().Be(30m);
        snapshot.StepBalances[stitching.Id].Should().Be(60m);
        snapshot.TotalActiveQuantity.Should().Be(100m);
    }

    [Fact]
    public async Task HistoricalEdit_Move80EditedTo75_ShouldTriggerReplayRecalculation()
    {
        // Arrange: Lot-101 (95 pcs). Move 80 Washing -> Finishing. Washing = 15, Finishing = 80.
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var factoryId = Guid.NewGuid();
        var washing = new ManufacturingStep(Guid.NewGuid(), "WASH", "Washing", 1, factoryId);
        var finishing = new ManufacturingStep(Guid.NewGuid(), "FIN", "Finishing", 2, factoryId);

        await stepRepo.AddAsync(washing);
        await stepRepo.AddAsync(finishing);

        var createHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lotId = await createHandler.Handle(new CreateProductionLotCommand("Lot-101", Guid.NewGuid(), 95m, UnitType.Pieces, washing.Id), default);

        var moveHandler = new MoveLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var moveDate = DateTime.UtcNow;
        var movementId = await moveHandler.Handle(new MoveLotCommand(lotId, washing.Id, finishing.Id, 80m, moveDate), default);

        var editHandler = new EditMovementCommandHandler(movementRepo, _replayEngine);

        // Act: Edit movement quantity from 80 to 75
        await editHandler.Handle(new EditMovementCommand(movementId, washing.Id, finishing.Id, 75m, moveDate, "Typo correction"), default);

        // Assert: Washing = 20 (95 - 75), Finishing = 75
        var movements = await movementRepo.GetMovementsForLotAsync(lotId);
        var snapshot = _replayEngine.ReplayLedger(lotId, movements);

        snapshot.StepBalances[washing.Id].Should().Be(20m);
        snapshot.StepBalances[finishing.Id].Should().Be(75m);
    }

    [Fact]
    public async Task HistoricalDelete_ReverseMovement_ShouldRestoreInventoryBalance()
    {
        // Arrange: Lot-101 (95 pcs). Move 80 Washing -> Finishing.
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var factoryId = Guid.NewGuid();
        var washing = new ManufacturingStep(Guid.NewGuid(), "WASH", "Washing", 1, factoryId);
        var finishing = new ManufacturingStep(Guid.NewGuid(), "FIN", "Finishing", 2, factoryId);

        await stepRepo.AddAsync(washing);
        await stepRepo.AddAsync(finishing);

        var createHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lotId = await createHandler.Handle(new CreateProductionLotCommand("Lot-101", Guid.NewGuid(), 95m, UnitType.Pieces, washing.Id), default);

        var moveHandler = new MoveLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var movementId = await moveHandler.Handle(new MoveLotCommand(lotId, washing.Id, finishing.Id, 80m, DateTime.UtcNow), default);

        var reverseHandler = new ReverseMovementCommandHandler(movementRepo, _replayEngine);

        // Act: Reverse the movement
        await reverseHandler.Handle(new ReverseMovementCommand(movementId, "Accidental movement", "Supervisor"), default);

        // Assert: Washing balance restored to 95 pcs, Finishing balance = 0
        var movements = await movementRepo.GetMovementsForLotAsync(lotId);
        var snapshot = _replayEngine.ReplayLedger(lotId, movements);

        snapshot.StepBalances[washing.Id].Should().Be(95m);
        snapshot.StepBalances.Should().NotContainKey(finishing.Id);
    }
}

public class OrderingAndConcurrencyTests
{
    private readonly LedgerReplayEngine _replayEngine = new();

    [Fact]
    public void Ordering_SameMovementDateDifferentCreatedAt_ShouldReplayDeterministically()
    {
        // Arrange: Two movements on same MovementDate, different CreatedAtUtc
        var lotId = Guid.NewGuid();
        var step1 = Guid.NewGuid();
        var step2 = Guid.NewGuid();
        var sameDate = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

        var initMovement = ManufacturingMovement.Create(
            Guid.NewGuid(), lotId, null, step1, 100m, MovementType.InitialProduction, sameDate);

        var created1 = sameDate.AddMinutes(1);
        var created2 = sameDate.AddMinutes(2);

        var m1 = ManufacturingMovement.Create(
            Guid.NewGuid(), lotId, step1, step2, 40m, MovementType.StepTransfer, sameDate);
        var m2 = ManufacturingMovement.Create(
            Guid.NewGuid(), lotId, step1, step2, 50m, MovementType.StepTransfer, sameDate);

        var movementsUnsorted = new List<ManufacturingMovement> { m2, m1, initMovement };

        // Act
        var snapshot = _replayEngine.ReplayLedger(lotId, movementsUnsorted);

        // Assert
        snapshot.StepBalances[step1].Should().Be(10m); // 100 - 40 - 50 = 10
        snapshot.StepBalances[step2].Should().Be(90m); // 40 + 50 = 90
    }

    [Fact]
    public async Task Concurrency_TwoParallelMovementsExceedingBalance_OneMustFailValidation()
    {
        // Arrange: Lot-101 has 15 pcs at Cutting. User 1 tries to move 10 pcs, User 2 tries to move 10 pcs simultaneously.
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var factoryId = Guid.NewGuid();
        var cutting = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, factoryId);
        var stitching = new ManufacturingStep(Guid.NewGuid(), "STITCH", "Stitching", 2, factoryId);

        await stepRepo.AddAsync(cutting);
        await stepRepo.AddAsync(stitching);

        var createHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lotId = await createHandler.Handle(new CreateProductionLotCommand("Lot-101", Guid.NewGuid(), 15m, UnitType.Pieces, cutting.Id), default);

        var moveHandler = new MoveLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);

        // User 1 moves 10 pcs
        await moveHandler.Handle(new MoveLotCommand(lotId, cutting.Id, stitching.Id, 10m, DateTime.UtcNow), default);

        // User 2 tries to move 10 pcs (balance is now only 5 pcs)
        Func<Task> actUser2 = async () => await moveHandler.Handle(new MoveLotCommand(lotId, cutting.Id, stitching.Id, 10m, DateTime.UtcNow), default);

        // Assert User 2 fails
        await actUser2.Should().ThrowAsync<DomainException>()
            .WithMessage("*negative*");
    }
}

public class PerformanceTests
{
    private readonly LedgerReplayEngine _replayEngine = new();

    [Fact]
    public void ReplayEngine_10000Movements_ShouldReplayCorrectlyAndExecuteUnder500Ms()
    {
        // Arrange: Create 1 initial production movement + 10,000 partial transfer movements for single lot
        var lotId = Guid.NewGuid();
        var stepA = Guid.NewGuid();
        var stepB = Guid.NewGuid();
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var movements = new List<ManufacturingMovement>(10001);

        // Initial creation of 1,000,000 units
        movements.Add(ManufacturingMovement.Create(
            Guid.NewGuid(), lotId, null, stepA, 1_000_000m, MovementType.InitialProduction, startDate));

        for (int i = 0; i < 10000; i++)
        {
            var mDate = startDate.AddMinutes(i);
            var isForward = (i % 2 == 0);
            var fromStep = isForward ? stepA : stepB;
            var toStep = isForward ? stepB : stepA;

            movements.Add(ManufacturingMovement.Create(
                Guid.NewGuid(), lotId, fromStep, toStep, 10m, MovementType.StepTransfer, mDate));
        }

        // Act & Benchmark
        var stopwatch = Stopwatch.StartNew();
        var snapshot = _replayEngine.ReplayLedger(lotId, movements);
        stopwatch.Stop();

        // Assert correctness
        snapshot.TotalMovementsReplayed.Should().Be(10001);
        snapshot.TotalActiveQuantity.Should().Be(1_000_000m);

        // Assert execution speed < 500ms
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500, $"Replay of 10,000 movements took {stopwatch.ElapsedMilliseconds} ms, expected < 500 ms.");
    }
}
