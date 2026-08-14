using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.Services;

namespace TrackLord.Application.Commands;

public record ReverseMovementCommand(
    Guid MovementId,
    string Reason,
    string? User = null) : IRequest<Guid>;

public class ReverseMovementCommandHandler : IRequestHandler<ReverseMovementCommand, Guid>
{
    private readonly IManufacturingMovementRepository _movementRepository;
    private readonly ILedgerReplayEngine _replayEngine;

    public ReverseMovementCommandHandler(
        IManufacturingMovementRepository movementRepository,
        ILedgerReplayEngine replayEngine)
    {
        _movementRepository = movementRepository;
        _replayEngine = replayEngine;
    }

    public async Task<Guid> Handle(ReverseMovementCommand request, CancellationToken cancellationToken)
    {
        var movement = await _movementRepository.GetByIdAsync(request.MovementId, cancellationToken);
        if (movement is null) throw new DomainException($"Movement ID '{request.MovementId}' not found.");

        var reversalId = Guid.NewGuid();
        var reversalMovement = movement.CreateReversal(reversalId, request.Reason, request.User);

        var existingMovements = await _movementRepository.GetMovementsForLotAsync(movement.ProductionLotId, cancellationToken);
        var allMovements = existingMovements.Concat(new[] { reversalMovement });

        _replayEngine.ReplayLedger(movement.ProductionLotId, allMovements);

        await _movementRepository.UpdateAsync(movement, cancellationToken);
        await _movementRepository.AddAsync(reversalMovement, cancellationToken);

        return reversalId;
    }
}
