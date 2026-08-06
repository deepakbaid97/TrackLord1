using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TrackLord.Application.Commands;
using TrackLord.Domain.Aggregates.ClientAggregate;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Exceptions;
using TrackLord.Infrastructure.Persistence.Repositories;
using TrackLord.Infrastructure.Services;
using Xunit;

namespace TrackLord.Tests;

public class SplitInventoryAndMultiStepTests
{
    private readonly LedgerReplayEngine _replayEngine = new();

    [Fact]
    public async Task SplitInventory_ShouldMaintainSingleLotIdentityAcrossMultipleStepsSimultaneously()
    {
        // Arrange: Lot-101 starts with 95 pcs at Washing
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var factoryId = Guid.NewGuid();
        var washingStep = new ManufacturingStep(Guid.NewGuid(), "WASH", "Washing", 1, factoryId);
        var finishingStep = new ManufacturingStep(Guid.NewGuid(), "FIN", "Finishing", 2, factoryId);
        await stepRepo.AddAsync(washingStep);
        await stepRepo.AddAsync(finishingStep);

        var createHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lotId = await createHandler.Handle(new CreateProductionLotCommand("Lot-101", Guid.NewGuid(), 95m, UnitType.Pieces, washingStep.Id), default);

        var moveHandler = new MoveLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);

        // Act: Move 80 pieces from Washing to Finishing
        await moveHandler.Handle(new MoveLotCommand(lotId, washingStep.Id, finishingStep.Id, 80m, DateTime.UtcNow), default);

        // Assert: Washing = 15, Finishing = 80. Lot Number is still Lot-101!
        var lot = await lotRepo.GetByIdAsync(lotId);
        lot!.LotNumber.Value.Should().Be("Lot-101");

        var movements = await movementRepo.GetMovementsForLotAsync(lotId);
        var snapshot = _replayEngine.ReplayLedger(lotId, movements);

        snapshot.StepBalances[washingStep.Id].Should().Be(15m);
        snapshot.StepBalances[finishingStep.Id].Should().Be(80m);
        snapshot.TotalActiveQuantity.Should().Be(95m);

        // Move remaining 15 pieces later
        await moveHandler.Handle(new MoveLotCommand(lotId, washingStep.Id, finishingStep.Id, 15m, DateTime.UtcNow), default);

        var finalMovements = await movementRepo.GetMovementsForLotAsync(lotId);
        var finalSnapshot = _replayEngine.ReplayLedger(lotId, finalMovements);

        finalSnapshot.StepBalances.Should().NotContainKey(washingStep.Id);
        finalSnapshot.StepBalances[finishingStep.Id].Should().Be(95m);
    }

    [Fact]
    public async Task MultiStep_CuttingToStitchingToWashingToFinishingToPacking_ShouldTrackBalancesAtEveryStage()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var factoryId = Guid.NewGuid();
        var cutting = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, factoryId);
        var stitching = new ManufacturingStep(Guid.NewGuid(), "STITCH", "Stitching", 2, factoryId);
        var washing = new ManufacturingStep(Guid.NewGuid(), "WASH", "Washing", 3, factoryId);
        var finishing = new ManufacturingStep(Guid.NewGuid(), "FIN", "Finishing", 4, factoryId);
        var packing = new ManufacturingStep(Guid.NewGuid(), "PACK", "Packing", 5, factoryId);

        await stepRepo.AddAsync(cutting);
        await stepRepo.AddAsync(stitching);
        await stepRepo.AddAsync(washing);
        await stepRepo.AddAsync(finishing);
        await stepRepo.AddAsync(packing);

        var createHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lotId = await createHandler.Handle(new CreateProductionLotCommand("Lot-101", Guid.NewGuid(), 100m, UnitType.Pieces, cutting.Id), default);

        var moveHandler = new MoveLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);

        // Move 100 Cutting -> Stitching
        await moveHandler.Handle(new MoveLotCommand(lotId, cutting.Id, stitching.Id, 100m, DateTime.UtcNow), default);

        // Move 100 Stitching -> Washing
        await moveHandler.Handle(new MoveLotCommand(lotId, stitching.Id, washing.Id, 100m, DateTime.UtcNow), default);

        // Partial move 80 Washing -> Finishing
        await moveHandler.Handle(new MoveLotCommand(lotId, washing.Id, finishing.Id, 80m, DateTime.UtcNow), default);

        // Partial move 50 Finishing -> Packing
        await moveHandler.Handle(new MoveLotCommand(lotId, finishing.Id, packing.Id, 50m, DateTime.UtcNow), default);

        // Assert balances: Washing = 20, Finishing = 30, Packing = 50. Total = 100
        var movements = await movementRepo.GetMovementsForLotAsync(lotId);
        var snapshot = _replayEngine.ReplayLedger(lotId, movements);

        snapshot.StepBalances[washing.Id].Should().Be(20m);
        snapshot.StepBalances[finishing.Id].Should().Be(30m);
        snapshot.StepBalances[packing.Id].Should().Be(50m);
        snapshot.TotalActiveQuantity.Should().Be(100m);
    }
}

public class MaterialConsumptionTests
{
    [Fact]
    public async Task ConsumeMaterial_GreyClothAndButtons_ShouldDeductMaterialLotAndMaintainTraceability()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var prodLotRepo = new ProductionLotRepository(db);
        var matLotRepo = new MaterialLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var consumptionRepo = new MaterialConsumptionRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);
        var replayEngine = new LedgerReplayEngine();

        var cuttingStep = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, Guid.NewGuid());
        var stitchingStep = new ManufacturingStep(Guid.NewGuid(), "STITCH", "Stitching", 2, Guid.NewGuid());
        await stepRepo.AddAsync(cuttingStep);
        await stepRepo.AddAsync(stitchingStep);

        // Create Raw Material Lot Grey-001 (100 meters)
        var createMatHandler = new CreateMaterialLotCommandHandler(matLotRepo);
        var greyClothLotId = await createMatHandler.Handle(new CreateMaterialLotCommand("Grey-001", Guid.NewGuid(), 100m, UnitType.Meters), default);

        // Create Raw Material Lot Buttons-001 (500 pieces)
        var buttonLotId = await createMatHandler.Handle(new CreateMaterialLotCommand("Buttons-001", Guid.NewGuid(), 500m, UnitType.Pieces), default);

        // Create Production Lot Lot-101
        var createProdHandler = new CreateProductionLotCommandHandler(prodLotRepo, stepRepo, movementRepo, replayEngine);
        var prodLotId = await createProdHandler.Handle(new CreateProductionLotCommand("Lot-101", Guid.NewGuid(), 95m, UnitType.Pieces, cuttingStep.Id), default);

        var consumeHandler = new ConsumeMaterialCommandHandler(prodLotRepo, matLotRepo, stepRepo, consumptionRepo);

        // Act 1: Consume 30m Grey Cloth at Cutting
        await consumeHandler.Handle(new ConsumeMaterialCommand(prodLotId, greyClothLotId, cuttingStep.Id, 30m, UnitType.Meters, DateTime.UtcNow), default);

        // Act 2: Consume 100 Buttons at Stitching
        await consumeHandler.Handle(new ConsumeMaterialCommand(prodLotId, buttonLotId, stitchingStep.Id, 100m, UnitType.Pieces, DateTime.UtcNow), default);

        // Assert Material Lot Balances
        var greyClothLot = await matLotRepo.GetByIdAsync(greyClothLotId);
        greyClothLot!.CurrentQuantity.Value.Should().Be(70m); // 100 - 30 = 70

        var buttonLot = await matLotRepo.GetByIdAsync(buttonLotId);
        buttonLot!.CurrentQuantity.Value.Should().Be(400m); // 500 - 100 = 400

        // Assert Traceability
        var consumptions = await consumptionRepo.GetByProductionLotIdAsync(prodLotId);
        consumptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConsumeMaterial_BeyondAvailableBalance_ShouldThrowDomainException()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var prodLotRepo = new ProductionLotRepository(db);
        var matLotRepo = new MaterialLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var consumptionRepo = new MaterialConsumptionRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);
        var replayEngine = new LedgerReplayEngine();

        var step = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, Guid.NewGuid());
        await stepRepo.AddAsync(step);

        var createMatHandler = new CreateMaterialLotCommandHandler(matLotRepo);
        var matLotId = await createMatHandler.Handle(new CreateMaterialLotCommand("Buttons-002", Guid.NewGuid(), 50m, UnitType.Pieces), default);

        var createProdHandler = new CreateProductionLotCommandHandler(prodLotRepo, stepRepo, movementRepo, replayEngine);
        var prodLotId = await createProdHandler.Handle(new CreateProductionLotCommand("Lot-102", Guid.NewGuid(), 95m, UnitType.Pieces, step.Id), default);

        var consumeHandler = new ConsumeMaterialCommandHandler(prodLotRepo, matLotRepo, stepRepo, consumptionRepo);

        // Act - Consume 100 buttons when only 50 available
        var command = new ConsumeMaterialCommand(prodLotId, matLotId, step.Id, 100m, UnitType.Pieces, DateTime.UtcNow);
        Func<Task> act = async () => await consumeHandler.Handle(command, default);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Insufficient material balance*");
    }
}

public class RejectionTests
{
    private readonly LedgerReplayEngine _replayEngine = new();

    [Fact]
    public async Task RejectItems_ShouldReduceLotWipBalanceExplicitly()
    {
        // Arrange: Lot-101 has 95 pcs at Washing
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);
        var rejectionRepo = new RejectionRecordRepository(db);

        var washingStep = new ManufacturingStep(Guid.NewGuid(), "WASH", "Washing", 1, Guid.NewGuid());
        await stepRepo.AddAsync(washingStep);

        var createHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lotId = await createHandler.Handle(new CreateProductionLotCommand("Lot-101", Guid.NewGuid(), 95m, UnitType.Pieces, washingStep.Id), default);

        var rejectHandler = new RejectItemsCommandHandler(lotRepo, stepRepo, movementRepo, rejectionRepo, _replayEngine);

        // Act: Reject 5 pieces at Washing due to stain defect
        await rejectHandler.Handle(new RejectItemsCommand(lotId, washingStep.Id, 5m, "Stain defect during wash", DateTime.UtcNow, CreatedBy: "QC_Inspector"), default);

        // Assert: Washing balance becomes 90 pcs (not 95!)
        var movements = await movementRepo.GetMovementsForLotAsync(lotId);
        var snapshot = _replayEngine.ReplayLedger(lotId, movements);

        snapshot.StepBalances[washingStep.Id].Should().Be(90m);
        snapshot.TotalActiveQuantity.Should().Be(90m);

        var rejections = await rejectionRepo.GetByProductionLotIdAsync(lotId);
        rejections.Should().HaveCount(1);
        rejections[0].QuantityRejected.Should().Be(5m);
        rejections[0].Reason.Should().Be("Stain defect during wash");
    }

    [Fact]
    public async Task RejectItems_ShouldTransferItemsToTargetStep_KeepLotQuantityIntact_AndSupportReversal()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);
        var rejectionRepo = new RejectionRecordRepository(db);
        var clientRepo = new ClientRepository(db);

        var client = new Client(Guid.NewGuid(), "Acme Textiles", "+91 98765 00000", "Buyer");
        await clientRepo.AddAsync(client);

        var washingStep = new ManufacturingStep(Guid.NewGuid(), "WASH", "Washing", 1, Guid.NewGuid());
        var finishingStep = new ManufacturingStep(Guid.NewGuid(), "FIN", "Finishing", 2, Guid.NewGuid());
        await stepRepo.AddAsync(washingStep);
        await stepRepo.AddAsync(finishingStep);

        var createLot = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lotId = await createLot.Handle(new CreateProductionLotCommand("Lot-202", Guid.NewGuid(), 100m, UnitType.Pieces, washingStep.Id), default);

        var moveLot = new MoveLotCommandHandler(lotRepo, stepRepo, movementRepo, clientRepo, _replayEngine);
        await moveLot.Handle(new MoveLotCommand(lotId, washingStep.Id, finishingStep.Id, 100m, DateTime.UtcNow), default);

        // Act 1: Reject 5 items at Finishing and transfer back to Washing with ClientId
        var rejectHandler = new RejectItemsCommandHandler(lotRepo, stepRepo, movementRepo, rejectionRepo, clientRepo, _replayEngine);
        await rejectHandler.Handle(new RejectItemsCommand(
            lotId,
            StepId: finishingStep.Id,
            QuantityRejected: 5m,
            Reason: "Stain found during finishing - return to washing",
            RejectionDate: DateTime.UtcNow,
            TargetStepId: washingStep.Id,
            ClientId: client.Id,
            CreatedBy: "QC_Inspector"), default);

        // Assert 1: Total quantity remains 100, Finishing has 95, Washing has 5
        var movements1 = await movementRepo.GetMovementsForLotAsync(lotId);
        var snapshot1 = _replayEngine.ReplayLedger(lotId, movements1);
        snapshot1.TotalActiveQuantity.Should().Be(100m);
        snapshot1.StepBalances[finishingStep.Id].Should().Be(95m);
        snapshot1.StepBalances[washingStep.Id].Should().Be(5m);

        // Act 2: Reverse the rejection movement from ledger
        var rejectionMovement = movements1.First(m => m.Type == MovementType.Rejection);
        var reverseHandler = new ReverseMovementCommandHandler(movementRepo, _replayEngine);
        await reverseHandler.Handle(new ReverseMovementCommand(rejectionMovement.Id, "Reversal test", "Supervisor"), default);

        // Assert 2: Reversal restores 5 items back to Finishing, Washing balance becomes 0
        var movements2 = await movementRepo.GetMovementsForLotAsync(lotId);
        var snapshot2 = _replayEngine.ReplayLedger(lotId, movements2);
        snapshot2.TotalActiveQuantity.Should().Be(100m);
        snapshot2.StepBalances[finishingStep.Id].Should().Be(100m);
        snapshot2.StepBalances.ContainsKey(washingStep.Id).Should().BeFalse();
    }
}
