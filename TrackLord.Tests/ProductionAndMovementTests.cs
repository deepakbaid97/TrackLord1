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

        // Assert
        Func<Task> act = async () => await handler.Handle(command, default);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task ImportProductionLots_WithExistingLotNumber_ShouldAutoIncrementSuffixAndAssociateClient()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);
        var clientRepo = new ClientRepository(db);

        var importHandler = new ImportProductionLotsCommandHandler(lotRepo, stepRepo, movementRepo, clientRepo, _replayEngine);

        // First import
        var items1 = new System.Collections.Generic.List<ImportLotItemDto>
        {
            new ImportLotItemDto("Apex Textiles", 100m, "LOT-001")
        };
        var result1 = await importHandler.Handle(new ImportProductionLotsCommand(items1), default);
        result1.ImportedLots[0].FinalLotNumber.Should().Be("LOT-001");

        // Second import with duplicate lot number LOT-001
        var items2 = new System.Collections.Generic.List<ImportLotItemDto>
        {
            new ImportLotItemDto("Apex Textiles", 250m, "LOT-001"),
            new ImportLotItemDto("Apex Textiles", 300m, "LOT-001")
        };
        var result2 = await importHandler.Handle(new ImportProductionLotsCommand(items2), default);

        // Assert auto-increment suffix
        result2.ImportedLots[0].FinalLotNumber.Should().Be("LOT-001-1");
        result2.ImportedLots[1].FinalLotNumber.Should().Be("LOT-001-2");

        // Verify step 'IMPORT' was created and client associated
        var importStep = await stepRepo.GetByCodeAsync("IMPORT");
        importStep.Should().NotBeNull();

        var clients = await clientRepo.GetAllAsync();
        clients.Should().ContainSingle(c => c.Name == "Apex Textiles");

        // Verify ProductionLot itself contains the ClientId and ClientName
        var savedLot1 = await lotRepo.GetByIdAsync(result1.ImportedLots[0].LotId);
        savedLot1.Should().NotBeNull();
        savedLot1!.ClientId.Should().Be(clients[0].Id);
        savedLot1.ClientName.Should().Be("Apex Textiles");
    }

    [Fact]
    public async Task ImportProductionLots_WithSpecifiedStep_ShouldAssignCorrectStep()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);
        var clientRepo = new ClientRepository(db);

        // Pre-create a step "Cutting"
        var cuttingStep = new ManufacturingStep(Guid.NewGuid(), "CUTTING", "Cutting", 1, Guid.NewGuid());
        await stepRepo.AddAsync(cuttingStep);

        var importHandler = new ImportProductionLotsCommandHandler(lotRepo, stepRepo, movementRepo, clientRepo, _replayEngine);

        var items = new System.Collections.Generic.List<ImportLotItemDto>
        {
            new ImportLotItemDto("Apex Textiles", 100m, "LOT-101", StepId: cuttingStep.Id),
            new ImportLotItemDto("Apex Textiles", 200m, "LOT-102", StepName: "Stitching")
        };

        // Act
        var result = await importHandler.Handle(new ImportProductionLotsCommand(items), default);

        // Assert
        result.TotalImported.Should().Be(2);

        var lot1 = await lotRepo.GetByIdAsync(result.ImportedLots[0].LotId);
        lot1.Should().NotBeNull();
        lot1!.InitialStepId.Should().Be(cuttingStep.Id);
        lot1.ClientName.Should().Be("Apex Textiles");

        var lot2 = await lotRepo.GetByIdAsync(result.ImportedLots[1].LotId);
        lot2.Should().NotBeNull();
        var stitchingStep = await stepRepo.GetByCodeAsync("STITCHING");
        stitchingStep.Should().NotBeNull();
        lot2!.InitialStepId.Should().Be(stitchingStep!.Id);
        lot2.ClientName.Should().Be("Apex Textiles");
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

    [Fact]
    public async Task ReplayLedger_WithTerminatingStep_ShouldSeparateActiveAndTerminatedQuantities()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var factoryId = Guid.NewGuid();
        var cuttingStep = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, factoryId, isTerminating: false);
        var packingStep = new ManufacturingStep(Guid.NewGuid(), "PACK", "Packing", 2, factoryId, isTerminating: true);
        await stepRepo.AddAsync(cuttingStep);
        await stepRepo.AddAsync(packingStep);

        var createHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lotId = await createHandler.Handle(new CreateProductionLotCommand("Lot-101", Guid.NewGuid(), 100m, UnitType.Pieces, cuttingStep.Id), default);

        var moveHandler = new MoveLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        await moveHandler.Handle(new MoveLotCommand(lotId, cuttingStep.Id, packingStep.Id, 40m, DateTime.UtcNow), default);

        // Act
        var movements = await movementRepo.GetMovementsForLotAsync(lotId);
        var terminatingStepIds = new System.Collections.Generic.HashSet<Guid> { packingStep.Id };
        var snapshot = _replayEngine.ReplayLedger(lotId, movements, terminatingStepIds);

        // Assert
        snapshot.StepBalances[cuttingStep.Id].Should().Be(60m);
        snapshot.StepBalances[packingStep.Id].Should().Be(40m);
        snapshot.TotalActiveQuantity.Should().Be(60m);
        snapshot.TotalTerminatedQuantity.Should().Be(40m);
    }
}
