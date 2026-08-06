using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Aggregates.ClientAggregate;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Commands;

public record ClientDto(
    Guid Id,
    string Name,
    string PhoneNumber,
    string ClientType,
    DateTime CreatedAtUtc);

// 1. Get All Clients Query
public record GetAllClientsQuery() : IRequest<IReadOnlyList<ClientDto>>;

public class GetAllClientsQueryHandler : IRequestHandler<GetAllClientsQuery, IReadOnlyList<ClientDto>>
{
    private readonly IClientRepository _clientRepository;

    public GetAllClientsQueryHandler(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    public async Task<IReadOnlyList<ClientDto>> Handle(GetAllClientsQuery request, CancellationToken cancellationToken)
    {
        var clients = await _clientRepository.GetAllAsync(cancellationToken);
        return clients.Select(c => new ClientDto(c.Id, c.Name, c.PhoneNumber, c.ClientType, c.CreatedAtUtc)).ToList();
    }
}

// 2. Create Client Command
public record CreateClientCommand(
    string Name,
    string PhoneNumber,
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
            request.PhoneNumber,
            request.ClientType);

        await _clientRepository.AddAsync(client, cancellationToken);
        return client.Id;
    }
}
