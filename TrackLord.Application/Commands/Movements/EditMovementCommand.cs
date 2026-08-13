using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.Services;

namespace TrackLord.Application.Commands;

public record EditMovementCommand(
    Guid MovementId,
    Guid? FromStepId,
    Guid ToStepId,
    decimal NewQuantity,
    DateTime NewMovementDate,
    string? Remarks = null,
    string? ReferenceNumber = null) : IRequest<bool>;

public class EditMovementCommandHandler : IRequestHandler<EditMovementCommand, bool>
{
    private readonly IManufacturingMovementRepository _movementRepository;
    private readonly ILedgerReplayEngine _replayEngine;

    public EditMovementCommandHandler(
        IManufacturingMovementRepository movementRepository,
        ILedgerReplayEngine replayEngine)
    {
        _movementRepository = movementRepository;
        _replayEngine = replayEngine;
    }

    public async Task<bool> Handle(EditMovementCommand request, CancellationToken cancellationToken)
    {
        var movement = await _movementRepository.GetByIdAsync(request.MovementId, cancellationToken);
        if (movement is null) throw new DomainException($"Movement ID '{request.MovementId}' not found.");

        movement.EditMovement(
            request.FromStepId,
            request.ToStepId,
            request.NewQuantity,
            request.NewMovementDate,
            request.Remarks,
            request.ReferenceNumber);

        var allMovements = await _movementRepository.GetMovementsForLotAsync(movement.ProductionLotId, cancellationToken);
        _replayEngine.ReplayLedger(movement.ProductionLotId, allMovements);

        await _movementRepository.UpdateAsync(movement, cancellationToken);
        return true;
    }
}
