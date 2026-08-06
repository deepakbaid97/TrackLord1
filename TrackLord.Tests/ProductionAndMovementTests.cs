using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TrackLord.Application.Commands;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.ValueObjects;
using TrackLord.Infrastructure.Persistence;
using TrackLord.Infrastructure.Persistence.Repositories;
using TrackLord.Infrastructure.Services;
using Xunit;

namespace TrackLord.Tests;

public class TestDbContextFactory
{
    public static TrackLordDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TrackLordDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new TrackLordDbContext(options);
    }
}

public class ProductionTests
{
    private readonly LedgerReplayEngine _replayEngine = new();

    [Fact]
    public async Task CreateProductionLot_WithValidData_ShouldSucceedAndEmitEvent()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var factoryId = Guid.NewGuid();
        var cuttingStep = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, factoryId);
        await stepRepo.AddAsync(cuttingStep);

        var handler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var command = new CreateProductionLotCommand("Lot-101", Guid.NewGuid(), 95m, UnitType.Pieces, cuttingStep.Id, CreatedBy: "Operator1", Remarks: "Test batch");

        // Act
        var lotId = await handler.Handle(command, default);

        // Assert
        var createdLot = await lotRepo.GetByIdAsync(lotId);
        createdLot.Should().NotBeNull();
        createdLot!.LotNumber.Value.Should().Be("Lot-101");
        createdLot.InitialQuantity.Value.Should().Be(95m);
        createdLot.DomainEvents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateProductionLot_WithDuplicateLotNumber_ShouldThrowDomainException()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var cuttingStep = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, Guid.NewGuid());
        await stepRepo.AddAsync(cuttingStep);

        var handler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var command = new CreateProductionLotCommand("Lot-101", Guid.NewGuid(), 95m, UnitType.Pieces, cuttingStep.Id);

        await handler.Handle(command, default);

        // Act
        Func<Task> act = async () => await handler.Handle(command, default);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*already exists*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task CreateProductionLot_WithInvalidQuantity_ShouldThrowDomainException(decimal invalidQty)
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var cuttingStep = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, Guid.NewGuid());
        await stepRepo.AddAsync(cuttingStep);

        var handler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var command = new CreateProductionLotCommand("Lot-102", Guid.NewGuid(), invalidQty, UnitType.Pieces, cuttingStep.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, default);

        // Assert
        await act.Should().ThrowAsync<DomainException>();
    }
}

public class MovementTests
{
    private readonly LedgerReplayEngine _replayEngine = new();

    [Fact]
    public async Task MoveLot_FullMovement_ShouldMoveAllQuantityToNextStep()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var factoryId = Guid.NewGuid();
        var cuttingStep = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, factoryId);
        var stitchingStep = new ManufacturingStep(Guid.NewGuid(), "STITCH", "Stitching", 2, factoryId);
        await stepRepo.AddAsync(cuttingStep);
        await stepRepo.AddAsync(stitchingStep);

        var createHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lotId = await createHandler.Handle(new CreateProductionLotCommand("Lot-101", Guid.NewGuid(), 95m, UnitType.Pieces, cuttingStep.Id), default);

        var moveHandler = new MoveLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);

        // Act - Move all 95 pcs from Cutting to Stitching
        var moveCommand = new MoveLotCommand(lotId, cuttingStep.Id, stitchingStep.Id, 95m, DateTime.UtcNow);
        await moveHandler.Handle(moveCommand, default);

        // Assert
        var movements = await movementRepo.GetMovementsForLotAsync(lotId);
        var snapshot = _replayEngine.ReplayLedger(lotId, movements);

        snapshot.StepBalances.Should().NotContainKey(cuttingStep.Id);
        snapshot.StepBalances[stitchingStep.Id].Should().Be(95m);
    }

    [Fact]
    public async Task MoveLot_GreaterThanAvailableBalance_ShouldThrowDomainException()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var factoryId = Guid.NewGuid();
        var cuttingStep = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, factoryId);
        var stitchingStep = new ManufacturingStep(Guid.NewGuid(), "STITCH", "Stitching", 2, factoryId);
        await stepRepo.AddAsync(cuttingStep);
        await stepRepo.AddAsync(stitchingStep);

        var createHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lotId = await createHandler.Handle(new CreateProductionLotCommand("Lot-101", Guid.NewGuid(), 15m, UnitType.Pieces, cuttingStep.Id), default);

        var moveHandler = new MoveLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);

        // Act - Try to move 20 pcs when balance is only 15 pcs
        var moveCommand = new MoveLotCommand(lotId, cuttingStep.Id, stitchingStep.Id, 20m, DateTime.UtcNow);
        Func<Task> act = async () => await moveHandler.Handle(moveCommand, default);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*negative*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task MoveLot_ZeroOrNegativeQuantity_ShouldThrowDomainException(decimal invalidQty)
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var factoryId = Guid.NewGuid();
        var cuttingStep = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, factoryId);
        var stitchingStep = new ManufacturingStep(Guid.NewGuid(), "STITCH", "Stitching", 2, factoryId);
        await stepRepo.AddAsync(cuttingStep);
        await stepRepo.AddAsync(stitchingStep);

        var createHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lotId = await createHandler.Handle(new CreateProductionLotCommand("Lot-101", Guid.NewGuid(), 95m, UnitType.Pieces, cuttingStep.Id), default);

        var moveHandler = new MoveLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);

        var moveCommand = new MoveLotCommand(lotId, cuttingStep.Id, stitchingStep.Id, invalidQty, DateTime.UtcNow);
        Func<Task> act = async () => await moveHandler.Handle(moveCommand, default);

        await act.Should().ThrowAsync<DomainException>();
    }
}
