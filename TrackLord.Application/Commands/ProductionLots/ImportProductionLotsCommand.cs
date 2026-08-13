using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Aggregates.ClientAggregate;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.Services;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Application.Commands;

public record ImportLotItemDto(
    string? ClientName,
    decimal Quantity,
    string LotNumber,
    Guid? StepId = null,
    string? StepName = null);

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

        var existingSteps = await _stepRepository.GetAllAsync(cancellationToken);
        var stepById = existingSteps.ToDictionary(s => s.Id);
        var stepByNameOrCode = new Dictionary<string, ManufacturingStep>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in existingSteps)
        {
            stepByNameOrCode[s.Code.Trim()] = s;
            stepByNameOrCode[s.Name.Trim()] = s;
        }

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
                continue;
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

            ManufacturingStep targetStep = importStep;
            if (item.StepId.HasValue && stepById.TryGetValue(item.StepId.Value, out var stepFoundById))
            {
                targetStep = stepFoundById;
            }
            else if (!string.IsNullOrWhiteSpace(item.StepName))
            {
                string cleanStepName = item.StepName.Trim();
                if (stepByNameOrCode.TryGetValue(cleanStepName, out var stepFoundByName))
                {
                    targetStep = stepFoundByName;
                }
                else
                {
                    var currentSteps = await _stepRepository.GetAllAsync(cancellationToken);
                    int nextSeq = currentSteps.Count > 0 ? currentSteps.Max(s => s.SequenceOrder) + 1 : 1;
                    var defaultFactoryId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                    string code = cleanStepName.ToUpperInvariant().Replace(" ", "_");
                    if (code.Length > 20) code = code.Substring(0, 20);

                    targetStep = new ManufacturingStep(
                        Guid.NewGuid(),
                        code,
                        cleanStepName,
                        nextSeq,
                        defaultFactoryId,
                        isTerminating: false);
                    await _stepRepository.AddAsync(targetStep, cancellationToken);
                    stepById[targetStep.Id] = targetStep;
                    stepByNameOrCode[targetStep.Code] = targetStep;
                    stepByNameOrCode[targetStep.Name] = targetStep;
                }
            }

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
                targetStep.Id,
                request.CreatedBy,
                $"Imported production lot (Original: '{rawLotNumber}')");

            var initialMovement = ManufacturingMovement.Create(
                Guid.NewGuid(),
                lotId,
                fromStepId: null,
                toStepId: targetStep.Id,
                quantity: item.Quantity,
                type: MovementType.InitialProduction,
                movementDate: now,
                clientId: client.Id,
                clientName: client.Name,
                createdBy: request.CreatedBy,
                remarks: $"Initial imported lot creation movement to step '{targetStep.Name}'",
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
