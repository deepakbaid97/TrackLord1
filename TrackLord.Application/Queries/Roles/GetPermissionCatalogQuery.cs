using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Application.Queries;

public record GetPermissionCatalogQuery : IRequest<IReadOnlyList<PermissionDefinition>>;

public class GetPermissionCatalogQueryHandler : IRequestHandler<GetPermissionCatalogQuery, IReadOnlyList<PermissionDefinition>>
{
    public Task<IReadOnlyList<PermissionDefinition>> Handle(GetPermissionCatalogQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(PermissionCatalog.All);
    }
}
