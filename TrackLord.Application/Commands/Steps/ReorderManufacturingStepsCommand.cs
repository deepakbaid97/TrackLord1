using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Commands;

public record StepOrderDto(Guid Id, int SequenceOrder);

public record ReorderManufacturingStepsCommand(IReadOnlyList<StepOrderDto> Steps) : IRequest<bool>;

public class ReorderManufacturingStepsCommandHandler : IRequestHandler<ReorderManufacturingStepsCommand, bool>
{
    private readonly IManufacturingStepRepository _stepRepository;

    public ReorderManufacturingStepsCommandHandler(IManufacturingStepRepository stepRepository)
    {
        _stepRepository = stepRepository;
    }

    public async Task<bool> Handle(ReorderManufacturingStepsCommand request, CancellationToken cancellationToken)
    {
        if (request.Steps == null || request.Steps.Count == 0)
        {
            return true;
        }

        var stepsToUpdate = new List<ManufacturingStep>();
        foreach (var item in request.Steps)
        {
            var step = await _stepRepository.GetByIdAsync(item.Id, cancellationToken);
            if (step is not null)
            {
                step.UpdateSequenceOrder(item.SequenceOrder);
                stepsToUpdate.Add(step);
            }
        }

        if (stepsToUpdate.Count > 0)
        {
            await _stepRepository.UpdateRangeAsync(stepsToUpdate, cancellationToken);
        }

        return true;
    }
}
