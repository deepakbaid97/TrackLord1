using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Queries;

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
