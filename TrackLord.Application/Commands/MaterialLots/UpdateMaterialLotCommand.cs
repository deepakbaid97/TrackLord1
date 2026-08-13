using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Application.Commands;

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
