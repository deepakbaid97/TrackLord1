using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Commands;

public record UpdateProductionLotCommand(
    Guid Id,
    Guid? ClientId,
    string? ClientName,
    DateTime? CreatedAtUtc,
    string? Remarks = null) : IRequest<bool>;

public class UpdateProductionLotCommandHandler : IRequestHandler<UpdateProductionLotCommand, bool>
{
    private readonly IProductionLotRepository _lotRepository;
    private readonly IClientRepository _clientRepository;

    public UpdateProductionLotCommandHandler(
        IProductionLotRepository lotRepository,
        IClientRepository clientRepository)
    {
        _lotRepository = lotRepository;
        _clientRepository = clientRepository;
    }

    public async Task<bool> Handle(UpdateProductionLotCommand request, CancellationToken cancellationToken)
    {
        var lot = await _lotRepository.GetByIdAsync(request.Id, cancellationToken);
        if (lot == null)
        {
            throw new DomainException($"Production Lot ID '{request.Id}' not found.");
        }

        string? clientName = request.ClientName;
        if (request.ClientId.HasValue && request.ClientId.Value != Guid.Empty)
        {
            var client = await _clientRepository.GetByIdAsync(request.ClientId.Value, cancellationToken);
            if (client != null)
            {
                clientName = client.Name;
            }
        }
        else if (request.ClientId == null)
        {
            clientName = null;
        }

        lot.UpdateDetails(request.ClientId, clientName, request.CreatedAtUtc, request.Remarks);
        await _lotRepository.UpdateAsync(lot, cancellationToken);

        return true;
    }
}
