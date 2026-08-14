using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Aggregates.ClientAggregate;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Commands;

public record CreateClientCommand(
    string Name,
    string? PhoneNumber,
    string ClientType) : IRequest<Guid>;

public class CreateClientCommandHandler : IRequestHandler<CreateClientCommand, Guid>
{
    private readonly IClientRepository _clientRepository;

    public CreateClientCommandHandler(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    public async Task<Guid> Handle(CreateClientCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("Client name is required.");
        }

        var client = new Client(
            Guid.NewGuid(),
            request.Name,
            request.PhoneNumber ?? string.Empty,
            request.ClientType);

        await _clientRepository.AddAsync(client, cancellationToken);
        return client.Id;
    }
}
