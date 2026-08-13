using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Queries;

public record GetMaterialLotHistoryQuery(Guid MaterialLotId) : IRequest<MaterialLotHistoryDto>;

public class GetMaterialLotHistoryQueryHandler : IRequestHandler<GetMaterialLotHistoryQuery, MaterialLotHistoryDto>
{
    private readonly IMaterialLotRepository _materialLotRepository;
    private readonly IMaterialConsumptionRepository _consumptionRepository;
    private readonly IProductionLotRepository _productionLotRepository;
    private readonly IManufacturingStepRepository _stepRepository;

    public GetMaterialLotHistoryQueryHandler(
        IMaterialLotRepository materialLotRepository,
        IMaterialConsumptionRepository consumptionRepository,
        IProductionLotRepository productionLotRepository,
        IManufacturingStepRepository stepRepository)
    {
        _materialLotRepository = materialLotRepository;
        _consumptionRepository = consumptionRepository;
        _productionLotRepository = productionLotRepository;
        _stepRepository = stepRepository;
    }

    public async Task<MaterialLotHistoryDto> Handle(GetMaterialLotHistoryQuery request, CancellationToken cancellationToken)
    {
        var matLot = await _materialLotRepository.GetByIdAsync(request.MaterialLotId, cancellationToken);
        if (matLot is null) throw new DomainException($"Material Lot ID '{request.MaterialLotId}' not found.");

        var lotDto = new MaterialLotDto(
            matLot.Id,
            matLot.LotNumber.Value,
            matLot.MaterialItemId,
            matLot.MaterialName,
            matLot.InitialQuantity.Value,
            matLot.CurrentQuantity.Value,
            matLot.InitialQuantity.Unit.ToString(),
            matLot.UnitRate);

        var consumptions = await _consumptionRepository.GetByMaterialLotIdAsync(request.MaterialLotId, cancellationToken);
        var prodLots = (await _productionLotRepository.GetAllAsync(cancellationToken)).ToDictionary(l => l.Id);
        var steps = (await _stepRepository.GetAllAsync(cancellationToken)).ToDictionary(s => s.Id);

        var historyList = new List<MaterialConsumptionHistoryItemDto>();
        foreach (var c in consumptions)
        {
            prodLots.TryGetValue(c.ProductionLotId, out var prodLot);
            steps.TryGetValue(c.StepId, out var step);

            historyList.Add(new MaterialConsumptionHistoryItemDto(
                c.Id,
                c.ProductionLotId,
                prodLot?.LotNumber.Value ?? "UNKNOWN",
                c.StepId,
                step?.Name ?? "Unknown Step",
                c.QuantityConsumed,
                c.Unit.ToString(),
                c.UnitRate,
                c.TotalPrice,
                c.ConsumptionDate,
                c.CreatedAtUtc,
                c.CreatedBy,
                c.Remarks));
        }

        var sortedHistory = historyList.OrderByDescending(h => h.ConsumptionDate).ThenByDescending(h => h.CreatedAtUtc).ToList();
        var totalQtyConsumed = sortedHistory.Sum(h => h.QuantityConsumed);
        var totalCost = sortedHistory.Sum(h => h.TotalPrice);

        return new MaterialLotHistoryDto(
            lotDto,
            totalQtyConsumed,
            totalCost,
            sortedHistory.Count,
            sortedHistory);
    }
}
