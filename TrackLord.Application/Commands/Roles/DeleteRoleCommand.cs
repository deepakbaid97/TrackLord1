using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Commands;

public record DeleteRoleCommand(Guid Id) : IRequest;

public class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand>
{
    private readonly IRoleRepository _roleRepository;

    public DeleteRoleCommandHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.Id, cancellationToken);
        if (role == null)
        {
            throw new DomainException($"Role with ID '{request.Id}' was not found.");
        }

        if (role.IsSystemRole)
        {
            throw new DomainException($"System role '{role.Name}' cannot be deleted.");
        }

        var hasUsers = await _roleRepository.HasUsersAssignedAsync(role.Id, cancellationToken);
        if (hasUsers)
        {
            throw new DomainException($"Cannot delete role '{role.Name}' because team members are currently assigned to it. Please reassign them first.");
        }

        await _roleRepository.DeleteAsync(role, cancellationToken);
    }
}
