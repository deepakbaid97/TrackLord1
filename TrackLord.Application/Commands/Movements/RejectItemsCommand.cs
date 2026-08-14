using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Entities;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.Services;

namespace TrackLord.Application.Commands;

public record RejectItemsCommand(
    Guid ProductionLotId,
    Guid StepId,
    decimal QuantityRejected,
    string Reason,
    DateTime RejectionDate,
    Guid? TargetStepId = null,
    Guid? ClientId = null,
    string? ClientName = null,
    string? CreatedBy = null) : IRequest<Guid>;

public class RejectItemsCommandHandler : IRequestHandler<RejectItemsCommand, Guid>
{
    private readonly IProductionLotRepository _lotRepository;
    private readonly IManufacturingStepRepository _stepRepository;
    private readonly IManufacturingMovementRepository _movementRepository;
    private readonly IRejectionRecordRepository _rejectionRepository;
    private readonly IClientRepository _clientRepository;
    private readonly ILedgerReplayEngine _replayEngine;

    public RejectItemsCommandHandler(
        IProductionLotRepository lotRepository,
        IManufacturingStepRepository stepRepository,
        IManufacturingMovementRepository movementRepository,
        IRejectionRecordRepository rejectionRepository,
        ILedgerReplayEngine replayEngine)
        : this(lotRepository, stepRepository, movementRepository, rejectionRepository, null!, replayEngine)
    {
    }

    public RejectItemsCommandHandler(
        IProductionLotRepository lotRepository,
        IManufacturingStepRepository stepRepository,
        IManufacturingMovementRepository movementRepository,
        IRejectionRecordRepository rejectionRepository,
        IClientRepository clientRepository,
        ILedgerReplayEngine replayEngine)
    {
        _lotRepository = lotRepository;
        _stepRepository = stepRepository;
        _movementRepository = movementRepository;
        _rejectionRepository = rejectionRepository;
        _clientRepository = clientRepository;
        _replayEngine = replayEngine;
    }

    public async Task<Guid> Handle(RejectItemsCommand request, CancellationToken cancellationToken)
    {
        var lot = await _lotRepository.GetByIdAsync(request.ProductionLotId, cancellationToken);
        if (lot is null) throw new DomainException($"Production Lot ID '{request.ProductionLotId}' not found.");

        var step = await _stepRepository.GetByIdAsync(request.StepId, cancellationToken);
        if (step is null) throw new DomainException($"Step ID '{request.StepId}' not found.");

        var targetStepId = request.TargetStepId ?? request.StepId;
        var targetStep = await _stepRepository.GetByIdAsync(targetStepId, cancellationToken);
        if (targetStep is null) throw new DomainException($"Target return step ID '{targetStepId}' not found.");

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

        var rejectionMovement = ManufacturingMovement.Create(
            Guid.NewGuid(),
            request.ProductionLotId,
            fromStepId: request.StepId,
            toStepId: targetStepId,
            quantity: request.QuantityRejected,
            type: MovementType.Rejection,
            movementDate: request.RejectionDate,
            clientId: request.ClientId,
            clientName: clientName,
            createdBy: request.CreatedBy,
            remarks: $"Rejection: {request.Reason}",
            referenceNumber: $"REJ-{Math.Abs(Guid.NewGuid().GetHashCode()) % 10000:D4}");

        var allMovements = existingMovements.Concat(new[] { rejectionMovement });
        _replayEngine.ReplayLedger(request.ProductionLotId, allMovements);

        var rejectionRecord = RejectionRecord.Create(
            Guid.NewGuid(),
            request.ProductionLotId,
            request.StepId,
            targetStepId,
            request.QuantityRejected,
            request.Reason,
            request.RejectionDate,
            request.ClientId,
            request.CreatedBy);

        await _movementRepository.AddAsync(rejectionMovement, cancellationToken);
        await _rejectionRepository.AddAsync(rejectionRecord, cancellationToken);

        return rejectionRecord.Id;
    }
}
