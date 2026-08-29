using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Commands;

public record UpdateRoleCommand(
    Guid Id,
    string Name,
    string? Description,
    string? PermissionsJson,
    bool IsActive = true) : IRequest;

public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand>
{
    private readonly IRoleRepository _roleRepository;

    public UpdateRoleCommandHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.Id, cancellationToken);
        if (role == null)
        {
            throw new DomainException($"Role with ID '{request.Id}' was not found.");
        }

        // Check if name is taken by another role
        var existingWithSameName = await _roleRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existingWithSameName != null && existingWithSameName.Id != role.Id)
        {
            throw new DomainException($"Another role with name '{request.Name}' already exists.");
        }

        role.UpdateDetails(request.Name, request.Description, request.IsActive);
        if (request.PermissionsJson != null)
        {
            role.UpdatePermissions(request.PermissionsJson);
        }

        await _roleRepository.UpdateAsync(role, cancellationToken);
    }
}
