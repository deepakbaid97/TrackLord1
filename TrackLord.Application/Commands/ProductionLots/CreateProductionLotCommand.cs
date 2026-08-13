using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.Services;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Application.Commands;

public record CreateProductionLotCommand(
    string LotNumber,
    Guid ItemId,
    decimal InitialQuantity,
    UnitType Unit,
    Guid InitialStepId,
    DateTime? CreationDate = null,
    string? CreatedBy = null,
    string? Remarks = null,
    Guid? ClientId = null,
    string? ClientName = null) : IRequest<Guid>;

public class CreateProductionLotCommandHandler : IRequestHandler<CreateProductionLotCommand, Guid>
{
    private readonly IProductionLotRepository _lotRepository;
    private readonly IManufacturingStepRepository _stepRepository;
    private readonly IManufacturingMovementRepository _movementRepository;
    private readonly ILedgerReplayEngine _replayEngine;

    public CreateProductionLotCommandHandler(
        IProductionLotRepository lotRepository,
        IManufacturingStepRepository stepRepository,
        IManufacturingMovementRepository movementRepository,
        ILedgerReplayEngine replayEngine)
    {
        _lotRepository = lotRepository;
        _stepRepository = stepRepository;
        _movementRepository = movementRepository;
        _replayEngine = replayEngine;
    }

    public async Task<Guid> Handle(CreateProductionLotCommand request, CancellationToken cancellationToken)
    {
        if (await _lotRepository.ExistsAsync(request.LotNumber, cancellationToken))
        {
            throw new DomainException($"Production Lot Number '{request.LotNumber}' already exists. Lot numbers are unique and immutable.");
        }

        var step = await _stepRepository.GetByIdAsync(request.InitialStepId, cancellationToken);
        if (step is null)
        {
            throw new DomainException($"Initial manufacturing step with ID '{request.InitialStepId}' does not exist.");
        }

        var lotId = Guid.NewGuid();
        var lotNumberObj = ProductionLotNumber.Create(request.LotNumber);
        var initialQtyObj = new Quantity(request.InitialQuantity, request.Unit);

        var lot = ProductionLot.Create(
            lotId,
            lotNumberObj,
            request.ItemId,
            initialQtyObj,
            request.InitialStepId,
            request.CreatedBy,
            request.Remarks);

        var creationDate = request.CreationDate ?? DateTime.UtcNow;

        var initialMovement = ManufacturingMovement.Create(
            Guid.NewGuid(),
            lotId,
            fromStepId: null,
            toStepId: request.InitialStepId,
            quantity: request.InitialQuantity,
            type: MovementType.InitialProduction,
            movementDate: creationDate,
            clientId: request.ClientId,
            clientName: request.ClientName,
            createdBy: request.CreatedBy,
            remarks: request.Remarks ?? "Initial production lot creation movement",
            referenceNumber: $"INIT-{request.LotNumber}");

        _replayEngine.ReplayLedger(lotId, new[] { initialMovement });

        await _lotRepository.AddAsync(lot, cancellationToken);
        await _movementRepository.AddAsync(initialMovement, cancellationToken);

        return lotId;
    }
}
