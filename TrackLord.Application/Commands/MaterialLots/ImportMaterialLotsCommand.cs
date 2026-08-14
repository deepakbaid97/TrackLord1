using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Aggregates.MaterialLotAggregate;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Application.Commands;

public record ImportMaterialItemDto(
    string LotNumber,
    string? MaterialName,
    decimal InitialQuantity,
    UnitType Unit = UnitType.Meters,
    decimal UnitRate = 0m);

public record ImportMaterialLotsCommand(
    List<ImportMaterialItemDto> Items) : IRequest<ImportMaterialLotsResultDto>;

public record ImportMaterialLotsResultDto(
    int TotalImported,
    List<ImportedMaterialLotSummaryDto> ImportedMaterials);

public record ImportedMaterialLotSummaryDto(
    Guid Id,
    string OriginalLotNumber,
    string FinalLotNumber,
    string? MaterialName,
    decimal Quantity,
    UnitType Unit,
    decimal UnitRate);

public class ImportMaterialLotsCommandHandler : IRequestHandler<ImportMaterialLotsCommand, ImportMaterialLotsResultDto>
{
    private readonly IMaterialLotRepository _materialLotRepository;

    public ImportMaterialLotsCommandHandler(IMaterialLotRepository materialLotRepository)
    {
        _materialLotRepository = materialLotRepository;
    }

    public async Task<ImportMaterialLotsResultDto> Handle(ImportMaterialLotsCommand request, CancellationToken cancellationToken)
    {
        if (request.Items == null || !request.Items.Any())
        {
            return new ImportMaterialLotsResultDto(0, new List<ImportedMaterialLotSummaryDto>());
        }

        var defaultItemId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var batchUsedLotNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var importedSummaries = new List<ImportedMaterialLotSummaryDto>();

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.LotNumber) || item.InitialQuantity <= 0)
            {
                continue;
            }

            string rawLotNumber = item.LotNumber.Trim();
            string finalLotNumber = await ResolveUniqueMaterialLotNumber(rawLotNumber, batchUsedLotNumbers, cancellationToken);

            var lotId = Guid.NewGuid();
            var lotNumberObj = MaterialLotNumber.Create(finalLotNumber);
            var initialQtyObj = new Quantity(item.InitialQuantity, item.Unit);

            string materialName = string.IsNullOrWhiteSpace(item.MaterialName) ? "General Material" : item.MaterialName.Trim();

            var lot = MaterialLot.Create(
                lotId,
                lotNumberObj,
                defaultItemId,
                initialQtyObj,
                materialName,
                item.UnitRate < 0 ? 0m : item.UnitRate);

            await _materialLotRepository.AddAsync(lot, cancellationToken);

            importedSummaries.Add(new ImportedMaterialLotSummaryDto(
                lotId,
                rawLotNumber,
                finalLotNumber,
                materialName,
                item.InitialQuantity,
                item.Unit,
                item.UnitRate));
        }

        return new ImportMaterialLotsResultDto(importedSummaries.Count, importedSummaries);
    }

    private async Task<string> ResolveUniqueMaterialLotNumber(string baseLotNumber, HashSet<string> batchUsedLotNumbers, CancellationToken cancellationToken)
    {
        string candidate = baseLotNumber;
        if (!await _materialLotRepository.ExistsAsync(candidate, cancellationToken) && !batchUsedLotNumbers.Contains(candidate))
        {
            batchUsedLotNumbers.Add(candidate);
            return candidate;
        }

        int suffix = 1;
        while (true)
        {
            candidate = $"{baseLotNumber}-{suffix}";
            if (!await _materialLotRepository.ExistsAsync(candidate, cancellationToken) && !batchUsedLotNumbers.Contains(candidate))
            {
                batchUsedLotNumbers.Add(candidate);
                return candidate;
            }
            suffix++;
        }
    }
}
