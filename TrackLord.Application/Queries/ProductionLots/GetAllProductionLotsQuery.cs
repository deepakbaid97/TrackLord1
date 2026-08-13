using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TrackLord.Domain.Repositories;

namespace TrackLord.Application.Queries;

public record GetAllProductionLotsQuery : IRequest<IReadOnlyList<ProductionLotSummaryDto>>;

public class GetAllProductionLotsQueryHandler : IRequestHandler<GetAllProductionLotsQuery, IReadOnlyList<ProductionLotSummaryDto>>
{
    private readonly IProductionLotRepository _lotRepository;

    public GetAllProductionLotsQueryHandler(IProductionLotRepository lotRepository)
    {
        _lotRepository = lotRepository;
    }

    public async Task<IReadOnlyList<ProductionLotSummaryDto>> Handle(GetAllProductionLotsQuery request, CancellationToken cancellationToken)
    {
        var lots = await _lotRepository.GetAllAsync(cancellationToken);
        return lots.Select(l => new ProductionLotSummaryDto(
            l.Id,
            l.LotNumber.Value,
            l.InitialQuantity.Value,
            l.Status.ToString(),
            l.CreatedAtUtc
        )).OrderByDescending(l => l.CreatedAtUtc).ToList();
    }
}
