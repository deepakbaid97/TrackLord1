using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Commands;

public record AssignRoleToUserCommand(Guid UserId, Guid RoleId) : IRequest;

public class AssignRoleToUserCommandHandler : IRequestHandler<AssignRoleToUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;

    public AssignRoleToUserCommandHandler(IUserRepository userRepository, IRoleRepository roleRepository)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
    }

    public async Task Handle(AssignRoleToUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new DomainException($"User with ID '{request.UserId}' was not found.");
        }

        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
        if (role == null)
        {
            throw new DomainException($"Role with ID '{request.RoleId}' was not found.");
        }

        if (!role.IsActive)
        {
            throw new DomainException($"Role '{role.Name}' is deactivated and cannot be assigned.");
        }

        user.AssignRole(role.Id, role.Name);
        await _userRepository.UpdateAsync(user, cancellationToken);
    }
}
