using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Commands;

public record UpdateManufacturingStepCommand(
    Guid Id,
    string Name,
    int SequenceOrder,
    bool IsTerminating,
    bool IsActive) : IRequest<bool>;

public class UpdateManufacturingStepCommandHandler : IRequestHandler<UpdateManufacturingStepCommand, bool>
{
    private readonly IManufacturingStepRepository _stepRepository;

    public UpdateManufacturingStepCommandHandler(IManufacturingStepRepository stepRepository)
    {
        _stepRepository = stepRepository;
    }

    public async Task<bool> Handle(UpdateManufacturingStepCommand request, CancellationToken cancellationToken)
    {
        var step = await _stepRepository.GetByIdAsync(request.Id, cancellationToken);
        if (step is null)
        {
            throw new DomainException($"Manufacturing Step ID '{request.Id}' not found.");
        }

        step.UpdateDetails(request.Name, request.SequenceOrder, request.IsTerminating);
        if (request.IsActive) step.Activate();
        else step.Deactivate();

        await _stepRepository.UpdateAsync(step, cancellationToken);
        return true;
    }
}
