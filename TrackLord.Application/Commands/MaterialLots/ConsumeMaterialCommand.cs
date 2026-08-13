using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Aggregates.ConsumptionAggregate;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Commands;

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
