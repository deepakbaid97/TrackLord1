using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Aggregates.ClientAggregate;
using TrackLord.Domain.Aggregates.ConsumptionAggregate;
using TrackLord.Domain.Aggregates.MaterialLotAggregate;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Entities;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.Services;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Application.Commands;

// 1. Create Production Lot Command
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

        // Initial creation generates an InitialProduction movement to the initial step
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

        // Validate ledger replay
        _replayEngine.ReplayLedger(lotId, new[] { initialMovement });

        await _lotRepository.AddAsync(lot, cancellationToken);
        await _movementRepository.AddAsync(initialMovement, cancellationToken);

        return lotId;
    }
}

// 1b. Import Production Lots Command DTOs & Handler
public record ImportLotItemDto(
    string? ClientName,
    decimal Quantity,
    string LotNumber);

public record ImportProductionLotsCommand(
    List<ImportLotItemDto> Items,
    string? CreatedBy = null) : IRequest<ImportProductionLotsResultDto>;

public record ImportProductionLotsResultDto(
    int TotalImported,
    List<ImportedLotSummaryDto> ImportedLots);

public record ImportedLotSummaryDto(
    Guid LotId,
    string OriginalLotNumber,
    string FinalLotNumber,
    string ClientName,
    decimal Quantity);

public class ImportProductionLotsCommandHandler : IRequestHandler<ImportProductionLotsCommand, ImportProductionLotsResultDto>
{
    private readonly IProductionLotRepository _lotRepository;
    private readonly IManufacturingStepRepository _stepRepository;
    private readonly IManufacturingMovementRepository _movementRepository;
    private readonly IClientRepository _clientRepository;
    private readonly ILedgerReplayEngine _replayEngine;

    public ImportProductionLotsCommandHandler(
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

    public async Task<ImportProductionLotsResultDto> Handle(ImportProductionLotsCommand request, CancellationToken cancellationToken)
    {
        if (request.Items == null || !request.Items.Any())
        {
            return new ImportProductionLotsResultDto(0, new List<ImportedLotSummaryDto>());
        }

        // 1. Find or create the 'IMPORT' manufacturing step
        var importStep = await _stepRepository.GetByCodeAsync("IMPORT", cancellationToken);
        if (importStep == null)
        {
            var allSteps = await _stepRepository.GetAllAsync(cancellationToken);
            int nextSeq = allSteps.Count > 0 ? allSteps.Max(s => s.SequenceOrder) + 1 : 1;
            var defaultFactoryId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            importStep = new ManufacturingStep(
                Guid.NewGuid(),
                "IMPORT",
                "Import",
                nextSeq,
                defaultFactoryId,
                isTerminating: false);
            await _stepRepository.AddAsync(importStep, cancellationToken);
        }

        // 2. Load existing clients into memory for case-insensitive lookup
        var existingClients = await _clientRepository.GetAllAsync(cancellationToken);
        var clientMap = existingClients.ToDictionary(
            c => c.Name.Trim().ToLowerInvariant(),
            c => c,
            StringComparer.OrdinalIgnoreCase);

        var batchUsedLotNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var importedSummaries = new List<ImportedLotSummaryDto>();
        var defaultItemId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var now = DateTime.UtcNow;

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.LotNumber) || item.Quantity <= 0)
            {
                continue; // Skip invalid entries
            }

            string rawClientName = string.IsNullOrWhiteSpace(item.ClientName) ? "General Client" : item.ClientName.Trim();
            string clientKey = rawClientName.ToLowerInvariant();

            Client client;
            if (clientMap.TryGetValue(clientKey, out var foundClient))
            {
                client = foundClient;
            }
            else
            {
                client = new Client(Guid.NewGuid(), rawClientName, "", "Buyer");
                await _clientRepository.AddAsync(client, cancellationToken);
                clientMap[clientKey] = client;
            }

            // Resolve unique lot number (append -1, -2 if lot number already exists)
            string rawLotNumber = item.LotNumber.Trim();
            string finalLotNumber = await ResolveUniqueLotNumber(rawLotNumber, batchUsedLotNumbers, cancellationToken);

            var lotId = Guid.NewGuid();
            var lotNumberObj = ProductionLotNumber.Create(finalLotNumber);
            var initialQtyObj = new Quantity(item.Quantity, UnitType.Pieces);

            var lot = ProductionLot.Create(
                lotId,
                lotNumberObj,
                defaultItemId,
                initialQtyObj,
                importStep.Id,
                request.CreatedBy,
                $"Imported production lot (Original: '{rawLotNumber}')");

            var initialMovement = ManufacturingMovement.Create(
                Guid.NewGuid(),
                lotId,
                fromStepId: null,
                toStepId: importStep.Id,
                quantity: item.Quantity,
                type: MovementType.InitialProduction,
                movementDate: now,
                clientId: client.Id,
                clientName: client.Name,
                createdBy: request.CreatedBy,
                remarks: "Initial imported lot creation movement",
                referenceNumber: $"INIT-{finalLotNumber}");

            _replayEngine.ReplayLedger(lotId, new[] { initialMovement });

            await _lotRepository.AddAsync(lot, cancellationToken);
            await _movementRepository.AddAsync(initialMovement, cancellationToken);

            importedSummaries.Add(new ImportedLotSummaryDto(
                lotId,
                rawLotNumber,
                finalLotNumber,
                client.Name,
                item.Quantity));
        }

        return new ImportProductionLotsResultDto(importedSummaries.Count, importedSummaries);
    }

    private async Task<string> ResolveUniqueLotNumber(string baseLotNumber, HashSet<string> batchUsedLotNumbers, CancellationToken cancellationToken)
    {
        string candidate = baseLotNumber;
        if (!await _lotRepository.ExistsAsync(candidate, cancellationToken) && !batchUsedLotNumbers.Contains(candidate))
        {
            batchUsedLotNumbers.Add(candidate);
            return candidate;
        }

        int suffix = 1;
        while (true)
        {
            string testCandidate = $"{candidate}-{suffix}";
            if (!await _lotRepository.ExistsAsync(testCandidate, cancellationToken) && !batchUsedLotNumbers.Contains(testCandidate))
            {
                batchUsedLotNumbers.Add(testCandidate);
                return testCandidate;
            }
            suffix++;
        }
    }
}

// 2. Create Material Lot Command
public record CreateMaterialLotCommand(
    string LotNumber,
    Guid MaterialItemId,
    decimal InitialQuantity,
    UnitType Unit,
    string? MaterialName = null,
    decimal UnitRate = 0m) : IRequest<Guid>;

public class CreateMaterialLotCommandHandler : IRequestHandler<CreateMaterialLotCommand, Guid>
{
    private readonly IMaterialLotRepository _materialLotRepository;

    public CreateMaterialLotCommandHandler(IMaterialLotRepository materialLotRepository)
    {
        _materialLotRepository = materialLotRepository;
    }

    public async Task<Guid> Handle(CreateMaterialLotCommand request, CancellationToken cancellationToken)
    {
        if (await _materialLotRepository.ExistsAsync(request.LotNumber, cancellationToken))
        {
            throw new DomainException($"Material Lot Number '{request.LotNumber}' already exists.");
        }

        var lotId = Guid.NewGuid();
        var lotNumberObj = MaterialLotNumber.Create(request.LotNumber);
        var initialQtyObj = new Quantity(request.InitialQuantity, request.Unit);

        var lot = MaterialLot.Create(lotId, lotNumberObj, request.MaterialItemId, initialQtyObj, request.MaterialName, request.UnitRate);

        await _materialLotRepository.AddAsync(lot, cancellationToken);
        return lotId;
    }
}

// 2b. Update Material Lot Command
public record UpdateMaterialLotCommand(
    Guid Id,
    string LotNumber,
    decimal InitialQuantity,
    UnitType Unit,
    string? MaterialName = null,
    decimal UnitRate = 0m) : IRequest<bool>;

public class UpdateMaterialLotCommandHandler : IRequestHandler<UpdateMaterialLotCommand, bool>
{
    private readonly IMaterialLotRepository _materialLotRepository;

    public UpdateMaterialLotCommandHandler(IMaterialLotRepository materialLotRepository)
    {
        _materialLotRepository = materialLotRepository;
    }

    public async Task<bool> Handle(UpdateMaterialLotCommand request, CancellationToken cancellationToken)
    {
        var lot = await _materialLotRepository.GetByIdAsync(request.Id, cancellationToken);
        if (lot is null) throw new DomainException($"Material Lot ID '{request.Id}' not found.");

        var existingWithLotNum = await _materialLotRepository.GetByLotNumberAsync(request.LotNumber, cancellationToken);
        if (existingWithLotNum is not null && existingWithLotNum.Id != request.Id)
        {
            throw new DomainException($"Material Lot Number '{request.LotNumber}' is already used by another material lot.");
        }

        var lotNumberObj = MaterialLotNumber.Create(request.LotNumber);
        lot.UpdateDetails(lotNumberObj, request.MaterialName, request.InitialQuantity, request.Unit, request.UnitRate);

        await _materialLotRepository.UpdateAsync(lot, cancellationToken);
        return true;
    }
}

// 2c. Delete Material Lot Command
public record DeleteMaterialLotCommand(Guid MaterialLotId) : IRequest<bool>;

public class DeleteMaterialLotCommandHandler : IRequestHandler<DeleteMaterialLotCommand, bool>
{
    private readonly IMaterialLotRepository _materialLotRepository;
    private readonly IMaterialConsumptionRepository _consumptionRepository;

    public DeleteMaterialLotCommandHandler(
        IMaterialLotRepository materialLotRepository,
        IMaterialConsumptionRepository consumptionRepository)
    {
        _materialLotRepository = materialLotRepository;
        _consumptionRepository = consumptionRepository;
    }

    public async Task<bool> Handle(DeleteMaterialLotCommand request, CancellationToken cancellationToken)
    {
        var lot = await _materialLotRepository.GetByIdAsync(request.MaterialLotId, cancellationToken);
        if (lot is null) throw new DomainException($"Material Lot ID '{request.MaterialLotId}' not found.");

        var history = await _consumptionRepository.GetByMaterialLotIdAsync(request.MaterialLotId, cancellationToken);
        if (history.Count > 0)
        {
            throw new DomainException("It's not safe to delete this material lot because it has consumption history.");
        }

        await _materialLotRepository.DeleteAsync(lot, cancellationToken);
        return true;
    }
}

// 3. Move Lot Command (Supports full, partial, backdated movements)
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

        // Rule 3: Movement quantity must be positive
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

        // Deterministic Replay Engine Validation (Rule 4 & 13: Non-negative balance check across entire timeline)
        var allMovements = existingMovements.Concat(new[] { newMovement });
        _replayEngine.ReplayLedger(request.ProductionLotId, allMovements);

        await _movementRepository.AddAsync(newMovement, cancellationToken);
        return newMovement.Id;
    }
}

// 4. Consume Material Command
public record ConsumeMaterialCommand(
    Guid ProductionLotId,
    Guid MaterialLotId,
    Guid StepId,
    decimal QuantityConsumed,
    UnitType Unit,
    DateTime ConsumptionDate,
    string? CreatedBy = null,
    string? Remarks = null) : IRequest<Guid>;

public class ConsumeMaterialCommandHandler : IRequestHandler<ConsumeMaterialCommand, Guid>
{
    private readonly IProductionLotRepository _productionLotRepository;
    private readonly IMaterialLotRepository _materialLotRepository;
    private readonly IManufacturingStepRepository _stepRepository;
    private readonly IMaterialConsumptionRepository _consumptionRepository;

    public ConsumeMaterialCommandHandler(
        IProductionLotRepository productionLotRepository,
        IMaterialLotRepository materialLotRepository,
        IManufacturingStepRepository stepRepository,
        IMaterialConsumptionRepository consumptionRepository)
    {
        _productionLotRepository = productionLotRepository;
        _materialLotRepository = materialLotRepository;
        _stepRepository = stepRepository;
        _consumptionRepository = consumptionRepository;
    }

    public async Task<Guid> Handle(ConsumeMaterialCommand request, CancellationToken cancellationToken)
    {
        var prodLot = await _productionLotRepository.GetByIdAsync(request.ProductionLotId, cancellationToken);
        if (prodLot is null) throw new DomainException($"Production Lot ID '{request.ProductionLotId}' not found.");

        var matLot = await _materialLotRepository.GetByIdAsync(request.MaterialLotId, cancellationToken);
        if (matLot is null) throw new DomainException($"Material Lot ID '{request.MaterialLotId}' not found.");

        var step = await _stepRepository.GetByIdAsync(request.StepId, cancellationToken);
        if (step is null) throw new DomainException($"Step ID '{request.StepId}' not found.");

        // Rule 7: Consume from material lot (validates available balance)
        matLot.Consume(request.QuantityConsumed);

        var consumption = MaterialConsumption.Create(
            Guid.NewGuid(),
            request.ProductionLotId,
            request.MaterialLotId,
            request.StepId,
            request.QuantityConsumed,
            request.Unit,
            request.ConsumptionDate,
            request.CreatedBy,
            request.Remarks,
            matLot.UnitRate);

        await _materialLotRepository.UpdateAsync(matLot, cancellationToken);
        await _consumptionRepository.AddAsync(consumption, cancellationToken);

        return consumption.Id;
    }
}

// 4b. Edit Material Consumption Command
public record EditMaterialConsumptionCommand(
    Guid ConsumptionId,
    decimal NewQuantityConsumed,
    string? Remarks = null) : IRequest<bool>;

public class EditMaterialConsumptionCommandHandler : IRequestHandler<EditMaterialConsumptionCommand, bool>
{
    private readonly IMaterialConsumptionRepository _consumptionRepository;
    private readonly IMaterialLotRepository _materialLotRepository;

    public EditMaterialConsumptionCommandHandler(
        IMaterialConsumptionRepository consumptionRepository,
        IMaterialLotRepository materialLotRepository)
    {
        _consumptionRepository = consumptionRepository;
        _materialLotRepository = materialLotRepository;
    }

    public async Task<bool> Handle(EditMaterialConsumptionCommand request, CancellationToken cancellationToken)
    {
        var consumption = await _consumptionRepository.GetByIdAsync(request.ConsumptionId, cancellationToken);
        if (consumption is null)
        {
            throw new DomainException($"Material consumption with ID '{request.ConsumptionId}' not found.");
        }

        var matLot = await _materialLotRepository.GetByIdAsync(consumption.MaterialLotId, cancellationToken);
        if (matLot is null)
        {
            throw new DomainException($"Material lot with ID '{consumption.MaterialLotId}' not found.");
        }

        // Restore previous consumed quantity back to material lot
        matLot.Restore(consumption.QuantityConsumed);

        // Consume new quantity (validated by material lot domain entity)
        matLot.Consume(request.NewQuantityConsumed);

        // Update consumption entity
        consumption.Update(request.NewQuantityConsumed, request.Remarks);

        await _materialLotRepository.UpdateAsync(matLot, cancellationToken);
        await _consumptionRepository.UpdateAsync(consumption, cancellationToken);

        return true;
    }
}

// 5. Reject Items Command
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

// 6. Edit Movement Command (Triggers historical recalculation)
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

        // Replay all movements for the lot to validate historical edit
        var allMovements = await _movementRepository.GetMovementsForLotAsync(movement.ProductionLotId, cancellationToken);
        _replayEngine.ReplayLedger(movement.ProductionLotId, allMovements);

        await _movementRepository.UpdateAsync(movement, cancellationToken);
        return true;
    }
}

// 7. Reverse Movement Command (Soft delete / Reversal transaction)
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

// 8. Create Manufacturing Step Command
public record CreateManufacturingStepCommand(
    string Code,
    string Name,
    int SequenceOrder,
    bool IsTerminating = false,
    Guid? FactoryId = null) : IRequest<Guid>;

public class CreateManufacturingStepCommandHandler : IRequestHandler<CreateManufacturingStepCommand, Guid>
{
    private readonly IManufacturingStepRepository _stepRepository;

    public CreateManufacturingStepCommandHandler(IManufacturingStepRepository stepRepository)
    {
        _stepRepository = stepRepository;
    }

    public async Task<Guid> Handle(CreateManufacturingStepCommand request, CancellationToken cancellationToken)
    {
        var existingStep = await _stepRepository.GetByCodeAsync(request.Code, cancellationToken);
        if (existingStep is not null)
        {
            throw new DomainException($"Manufacturing Step Code '{request.Code}' already exists.");
        }

        var stepId = Guid.NewGuid();
        var factoryId = request.FactoryId ?? Guid.NewGuid();
        var step = new ManufacturingStep(stepId, request.Code, request.Name, request.SequenceOrder, factoryId, request.IsTerminating);

        await _stepRepository.AddAsync(step, cancellationToken);
        return stepId;
    }
}

// 9. Update Manufacturing Step Command
public record UpdateManufacturingStepCommand(
    Guid Id,
    string Name,
    int SequenceOrder,
    bool IsTerminating,
    bool IsActive) : IRequest<bool>;

public class UpdateManufacturingStepCommandHandler : IRequestHandler<UpdateManufacturingStepCommand, bool>
{
    private readonly IManufacturingStepRepository _stepRepository;

    public UpdateManufacturingStepCommandHandler(IManufacturingStepRepository stepRepository)
    {
        _stepRepository = stepRepository;
    }

    public async Task<bool> Handle(UpdateManufacturingStepCommand request, CancellationToken cancellationToken)
    {
        var step = await _stepRepository.GetByIdAsync(request.Id, cancellationToken);
        if (step is null)
        {
            throw new DomainException($"Manufacturing Step ID '{request.Id}' not found.");
        }

        step.UpdateDetails(request.Name, request.SequenceOrder, request.IsTerminating);
        if (request.IsActive) step.Activate();
        else step.Deactivate();

        await _stepRepository.UpdateAsync(step, cancellationToken);
        return true;
    }
}

// 12. Delete Production Lot Command
public record DeleteProductionLotCommand(Guid ProductionLotId) : IRequest<bool>;

public class DeleteProductionLotCommandHandler : IRequestHandler<DeleteProductionLotCommand, bool>
{
    private readonly IProductionLotRepository _productionLotRepository;
    private readonly IMaterialLotRepository _materialLotRepository;
    private readonly IMaterialConsumptionRepository _consumptionRepository;
    private readonly IManufacturingMovementRepository _movementRepository;
    private readonly IRejectionRecordRepository _rejectionRecordRepository;

    public DeleteProductionLotCommandHandler(
        IProductionLotRepository productionLotRepository,
        IMaterialLotRepository materialLotRepository,
        IMaterialConsumptionRepository consumptionRepository,
        IManufacturingMovementRepository movementRepository,
        IRejectionRecordRepository rejectionRecordRepository)
    {
        _productionLotRepository = productionLotRepository;
        _materialLotRepository = materialLotRepository;
        _consumptionRepository = consumptionRepository;
        _movementRepository = movementRepository;
        _rejectionRecordRepository = rejectionRecordRepository;
    }

    public async Task<bool> Handle(DeleteProductionLotCommand request, CancellationToken cancellationToken)
    {
        var lot = await _productionLotRepository.GetByIdAsync(request.ProductionLotId, cancellationToken);
        if (lot is null)
        {
            throw new DomainException($"Production Lot ID '{request.ProductionLotId}' not found.");
        }

        // 1. Reverse raw material consumptions & restore material lot balances
        var consumptions = await _consumptionRepository.GetByProductionLotIdAsync(request.ProductionLotId, cancellationToken);
        foreach (var c in consumptions)
        {
            var matLot = await _materialLotRepository.GetByIdAsync(c.MaterialLotId, cancellationToken);
            if (matLot is not null)
            {
                matLot.Restore(c.QuantityConsumed);
                await _materialLotRepository.UpdateAsync(matLot, cancellationToken);
            }
        }

        // Delete all material consumptions for this lot
        if (consumptions.Count > 0)
        {
            await _consumptionRepository.DeleteRangeAsync(consumptions, cancellationToken);
        }

        // 2. Delete all manufacturing movements related to this lot
        var movements = await _movementRepository.GetMovementsForLotAsync(request.ProductionLotId, cancellationToken);
        if (movements.Count > 0)
        {
            await _movementRepository.DeleteRangeAsync(movements, cancellationToken);
        }

        // 3. Delete all rejection records related to this lot
        var rejections = await _rejectionRecordRepository.GetByProductionLotIdAsync(request.ProductionLotId, cancellationToken);
        if (rejections.Count > 0)
        {
            await _rejectionRecordRepository.DeleteRangeAsync(rejections, cancellationToken);
        }

        // 4. Delete the production lot itself
        await _productionLotRepository.DeleteAsync(lot, cancellationToken);

        return true;
    }
}

