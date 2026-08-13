using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Commands;

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

        matLot.Restore(consumption.QuantityConsumed);

        matLot.Consume(request.NewQuantityConsumed);

        consumption.Update(request.NewQuantityConsumed, request.Remarks);

        await _materialLotRepository.UpdateAsync(matLot, cancellationToken);
        await _consumptionRepository.UpdateAsync(consumption, cancellationToken);

        return true;
    }
}
