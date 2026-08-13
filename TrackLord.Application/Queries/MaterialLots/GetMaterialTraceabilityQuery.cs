using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Queries;

public record GetMaterialTraceabilityQuery(Guid ProductionLotId) : IRequest<MaterialTraceabilityDto>;

public class GetMaterialTraceabilityQueryHandler : IRequestHandler<GetMaterialTraceabilityQuery, MaterialTraceabilityDto>
{
    private readonly IProductionLotRepository _lotRepository;
    private readonly IMaterialConsumptionRepository _consumptionRepository;
    private readonly IMaterialLotRepository _materialLotRepository;
    private readonly IManufacturingStepRepository _stepRepository;

    public GetMaterialTraceabilityQueryHandler(
        IProductionLotRepository lotRepository,
        IMaterialConsumptionRepository consumptionRepository,
        IMaterialLotRepository materialLotRepository,
        IManufacturingStepRepository stepRepository)
    {
        _lotRepository = lotRepository;
        _consumptionRepository = consumptionRepository;
        _materialLotRepository = materialLotRepository;
        _stepRepository = stepRepository;
    }

    public async Task<MaterialTraceabilityDto> Handle(GetMaterialTraceabilityQuery request, CancellationToken cancellationToken)
    {
        var lot = await _lotRepository.GetByIdAsync(request.ProductionLotId, cancellationToken);
        if (lot is null) throw new DomainException($"Production lot ID '{request.ProductionLotId}' not found.");

        var consumptions = await _consumptionRepository.GetByProductionLotIdAsync(request.ProductionLotId, cancellationToken);
        var steps = (await _stepRepository.GetAllAsync(cancellationToken)).ToDictionary(s => s.Id);

        var traces = new List<MaterialConsumptionTraceDto>();
        foreach (var c in consumptions)
        {
            var matLot = await _materialLotRepository.GetByIdAsync(c.MaterialLotId, cancellationToken);
            steps.TryGetValue(c.StepId, out var step);

            traces.Add(new MaterialConsumptionTraceDto(
                c.Id,
                c.MaterialLotId,
                matLot?.LotNumber.Value ?? "UNKNOWN",
                matLot?.MaterialName,
                c.StepId,
                step?.Name ?? "Unknown Step",
                c.QuantityConsumed,
                c.Unit.ToString(),
                c.UnitRate,
                c.TotalPrice,
                c.ConsumptionDate,
                c.Remarks));
        }

        var totalMaterialCost = traces.Sum(t => t.TotalPrice);
        return new MaterialTraceabilityDto(lot.Id, lot.LotNumber.Value, totalMaterialCost, traces);
    }
}
