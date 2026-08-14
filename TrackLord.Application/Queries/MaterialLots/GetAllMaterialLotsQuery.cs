using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Queries;

public record GetAllMaterialLotsQuery : IRequest<IReadOnlyList<MaterialLotDto>>;

public class GetAllMaterialLotsQueryHandler : IRequestHandler<GetAllMaterialLotsQuery, IReadOnlyList<MaterialLotDto>>
{
    private readonly IMaterialLotRepository _materialLotRepository;

    public GetAllMaterialLotsQueryHandler(IMaterialLotRepository materialLotRepository)
    {
        _materialLotRepository = materialLotRepository;
    }

    public async Task<IReadOnlyList<MaterialLotDto>> Handle(GetAllMaterialLotsQuery request, CancellationToken cancellationToken)
    {
        var lots = await _materialLotRepository.GetAllAsync(cancellationToken);
        return lots.Select(m => new MaterialLotDto(
            m.Id,
            m.LotNumber.Value,
            m.MaterialItemId,
            m.MaterialName,
            m.InitialQuantity.Value,
            m.CurrentQuantity.Value,
            m.InitialQuantity.Unit.ToString(),
            m.UnitRate)).OrderByDescending(m => m.Id).ToList();
    }
}
