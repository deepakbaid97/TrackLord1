using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Commands;

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

        if (consumptions.Count > 0)
        {
            await _consumptionRepository.DeleteRangeAsync(consumptions, cancellationToken);
        }

        var movements = await _movementRepository.GetMovementsForLotAsync(request.ProductionLotId, cancellationToken);
        if (movements.Count > 0)
        {
            await _movementRepository.DeleteRangeAsync(movements, cancellationToken);
        }

        var rejections = await _rejectionRecordRepository.GetByProductionLotIdAsync(request.ProductionLotId, cancellationToken);
        if (rejections.Count > 0)
        {
            await _rejectionRecordRepository.DeleteRangeAsync(rejections, cancellationToken);
        }

        await _productionLotRepository.DeleteAsync(lot, cancellationToken);

        return true;
    }
}
