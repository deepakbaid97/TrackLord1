using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Queries;

public record GetAllManufacturingStepsQuery : IRequest<IReadOnlyList<ManufacturingStepDto>>;

public class GetAllManufacturingStepsQueryHandler : IRequestHandler<GetAllManufacturingStepsQuery, IReadOnlyList<ManufacturingStepDto>>
{
    private readonly IManufacturingStepRepository _stepRepository;

    public GetAllManufacturingStepsQueryHandler(IManufacturingStepRepository stepRepository)
    {
        _stepRepository = stepRepository;
    }

    public async Task<IReadOnlyList<ManufacturingStepDto>> Handle(GetAllManufacturingStepsQuery request, CancellationToken cancellationToken)
    {
        var steps = await _stepRepository.GetAllAsync(cancellationToken);
        return steps.Select(s => new ManufacturingStepDto(
            s.Id,
            s.Code,
            s.Name,
            s.SequenceOrder,
            s.IsTerminating,
            s.FactoryId
        )).OrderBy(s => s.SequenceOrder).ToList();
    }
}
