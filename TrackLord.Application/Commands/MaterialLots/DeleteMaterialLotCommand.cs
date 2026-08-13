using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Commands;

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
