using System;
using System.Threading.Tasks;
using FluentAssertions;
using TrackLord.Application.Commands;
using TrackLord.Application.Queries;
using TrackLord.Domain.Aggregates.ClientAggregate;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Enums;
using TrackLord.Infrastructure.Persistence.Repositories;
using TrackLord.Infrastructure.Services;
using Xunit;

namespace TrackLord.Tests;

public class ClientTests
{
    private readonly LedgerReplayEngine _replayEngine = new();

    [Fact]
    public async Task CreateClient_AndMoveLotWithClientId_ShouldStoreClientIdAndResolveName()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var clientRepo = new ClientRepository(db);
        var lotRepo = new ProductionLotRepository(db);
        var stepRepo = new ManufacturingStepRepository(db);
        var movementRepo = new ManufacturingMovementRepository(db);

        var createClientHandler = new CreateClientCommandHandler(clientRepo);
        var clientId = await createClientHandler.Handle(new CreateClientCommand("Apex Textiles", "9876543210", "Wholesaler"), default);

        var getClientsHandler = new GetAllClientsQueryHandler(clientRepo);
        var clients = await getClientsHandler.Handle(new GetAllClientsQuery(), default);
        clients.Should().HaveCount(1);
        clients[0].Name.Should().Be("Apex Textiles");
        clients[0].PhoneNumber.Should().Be("9876543210");
        clients[0].ClientType.Should().Be("Wholesaler");

        // Create Steps & Lot
        var step1 = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, Guid.NewGuid());
        var step2 = new ManufacturingStep(Guid.NewGuid(), "SEW", "Sewing", 2, Guid.NewGuid());
        await stepRepo.AddAsync(step1);
        await stepRepo.AddAsync(step2);

        var createLotHandler = new CreateProductionLotCommandHandler(lotRepo, stepRepo, movementRepo, _replayEngine);
        var lotId = await createLotHandler.Handle(new CreateProductionLotCommand("LOT-2026-CLIENT", Guid.NewGuid(), 100, UnitType.Pieces, step1.Id), default);

        var moveHandler = new MoveLotCommandHandler(lotRepo, stepRepo, movementRepo, clientRepo, _replayEngine);

        // Act
        var moveCmd = new MoveLotCommand(lotId, step1.Id, step2.Id, 25, DateTime.UtcNow, ClientId: clientId);
        var movementId = await moveHandler.Handle(moveCmd, default);

        // Assert
        var savedMovement = await movementRepo.GetByIdAsync(movementId);
        savedMovement.Should().NotBeNull();
        savedMovement!.ClientId.Should().Be(clientId);
        savedMovement.ClientName.Should().Be("Apex Textiles");
    }
}
