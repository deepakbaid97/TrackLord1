using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Queries;

public record GetRoleByIdQuery(Guid Id) : IRequest<RoleDto?>;

public class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, RoleDto?>
{
    private readonly IRoleRepository _roleRepository;

    public GetRoleByIdQueryHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<RoleDto?> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.Id, cancellationToken);
        if (role == null) return null;

        return new RoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystemRole,
            role.IsActive,
            role.PermissionsJson,
            role.GetPermissionsDictionary(),
            role.CreatedAtUtc
        );
    }
}
