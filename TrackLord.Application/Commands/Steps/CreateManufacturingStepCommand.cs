using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Commands;

public record CreateManufacturingStepCommand(
    string Code,
    string Name,
    int SequenceOrder,
    bool IsTerminating = false,
    Guid? FactoryId = null) : IRequest<Guid>;

public class CreateManufacturingStepCommandHandler : IRequestHandler<CreateManufacturingStepCommand, Guid>
{
    private readonly IManufacturingStepRepository _stepRepository;

    public CreateManufacturingStepCommandHandler(IManufacturingStepRepository stepRepository)
    {
        _stepRepository = stepRepository;
    }

    public async Task<Guid> Handle(CreateManufacturingStepCommand request, CancellationToken cancellationToken)
    {
        var existingStep = await _stepRepository.GetByCodeAsync(request.Code, cancellationToken);
        if (existingStep is not null)
        {
            throw new DomainException($"Manufacturing Step Code '{request.Code}' already exists.");
        }

        var stepId = Guid.NewGuid();
        var factoryId = request.FactoryId ?? Guid.NewGuid();
        var step = new ManufacturingStep(stepId, request.Code, request.Name, request.SequenceOrder, factoryId, request.IsTerminating);

        await _stepRepository.AddAsync(step, cancellationToken);
        return stepId;
    }
}
