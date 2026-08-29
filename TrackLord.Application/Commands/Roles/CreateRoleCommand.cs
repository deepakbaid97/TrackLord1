using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Aggregates.RoleAggregate;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Commands;

public record CreateRoleCommand(
    string Name,
    string? Description,
    string? PermissionsJson) : IRequest<Guid>;

public class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, Guid>
{
    private readonly IRoleRepository _roleRepository;

    public CreateRoleCommandHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<Guid> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("Role name is required.");
        }

        var existing = await _roleRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existing != null)
        {
            throw new DomainException($"A role with name '{request.Name}' already exists.");
        }

        var role = new Role(
            Guid.NewGuid(),
            request.Name,
            request.Description,
            isSystemRole: false,
            permissionsJson: request.PermissionsJson);

        await _roleRepository.AddAsync(role, cancellationToken);
        return role.Id;
    }
}
