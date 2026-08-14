using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.Services;

namespace TrackLord.Application.Commands;

public record MoveLotCommand(
    Guid ProductionLotId,
    Guid FromStepId,
    Guid ToStepId,
    decimal Quantity,
    DateTime MovementDate,
    Guid? ClientId = null,
    string? ClientName = null,
    string? CreatedBy = null,
    string? Remarks = null,
    string? ReferenceNumber = null) : IRequest<Guid>;

public class MoveLotCommandHandler : IRequestHandler<MoveLotCommand, Guid>
{
    private readonly IProductionLotRepository _lotRepository;
    private readonly IManufacturingStepRepository _stepRepository;
    private readonly IManufacturingMovementRepository _movementRepository;
    private readonly IClientRepository _clientRepository;
    private readonly ILedgerReplayEngine _replayEngine;

    public MoveLotCommandHandler(
        IProductionLotRepository lotRepository,
        IManufacturingStepRepository stepRepository,
        IManufacturingMovementRepository movementRepository,
        ILedgerReplayEngine replayEngine)
        : this(lotRepository, stepRepository, movementRepository, null!, replayEngine)
    {
    }

    public MoveLotCommandHandler(
        IProductionLotRepository lotRepository,
        IManufacturingStepRepository stepRepository,
        IManufacturingMovementRepository movementRepository,
        IClientRepository clientRepository,
        ILedgerReplayEngine replayEngine)
    {
        _lotRepository = lotRepository;
        _stepRepository = stepRepository;
        _movementRepository = movementRepository;
        _clientRepository = clientRepository;
        _replayEngine = replayEngine;
    }

    public async Task<Guid> Handle(MoveLotCommand request, CancellationToken cancellationToken)
    {
        var lot = await _lotRepository.GetByIdAsync(request.ProductionLotId, cancellationToken);
        if (lot is null)
        {
            throw new DomainException($"Production lot with ID '{request.ProductionLotId}' does not exist.");
        }

        var fromStep = await _stepRepository.GetByIdAsync(request.FromStepId, cancellationToken);
        if (fromStep is null) throw new DomainException($"Source step ID '{request.FromStepId}' does not exist.");

        var toStep = await _stepRepository.GetByIdAsync(request.ToStepId, cancellationToken);
        if (toStep is null) throw new DomainException($"Target step ID '{request.ToStepId}' does not exist.");

        if (request.Quantity <= 0)
        {
            throw new DomainException($"Movement quantity must be positive. Provided: {request.Quantity}");
        }

        string? clientName = request.ClientName;
        if (_clientRepository != null && request.ClientId.HasValue && request.ClientId.Value != Guid.Empty)
        {
            var client = await _clientRepository.GetByIdAsync(request.ClientId.Value, cancellationToken);
            if (client != null)
            {
                clientName = client.Name;
            }
        }

        var existingMovements = await _movementRepository.GetMovementsForLotAsync(request.ProductionLotId, cancellationToken);

        var newMovement = ManufacturingMovement.Create(
            Guid.NewGuid(),
            request.ProductionLotId,
            request.FromStepId,
            request.ToStepId,
            request.Quantity,
            MovementType.StepTransfer,
            request.MovementDate,
            request.ClientId,
            clientName,
            request.CreatedBy,
            request.Remarks,
            request.ReferenceNumber);

        var allMovements = existingMovements.Concat(new[] { newMovement });
        _replayEngine.ReplayLedger(request.ProductionLotId, allMovements);

        await _movementRepository.AddAsync(newMovement, cancellationToken);
        return newMovement.Id;
    }
}
