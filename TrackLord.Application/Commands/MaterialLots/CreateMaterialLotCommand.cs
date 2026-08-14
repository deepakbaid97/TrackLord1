using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Aggregates.MaterialLotAggregate;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Application.Commands;

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
