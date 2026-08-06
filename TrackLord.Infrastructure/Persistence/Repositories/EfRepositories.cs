using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TrackLord.Domain.Aggregates.ConsumptionAggregate;
using TrackLord.Domain.Aggregates.MaterialLotAggregate;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Entities;
using TrackLord.Domain.Repositories;

namespace TrackLord.Infrastructure.Persistence.Repositories;

public class ProductionLotRepository : IProductionLotRepository
{
    private readonly TrackLordDbContext _dbContext;

    public ProductionLotRepository(TrackLordDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProductionLot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProductionLots.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<ProductionLot?> GetByLotNumberAsync(string lotNumber, CancellationToken cancellationToken = default)
    {
        var trimmed = lotNumber.Trim();
        return await _dbContext.ProductionLots.FirstOrDefaultAsync(p => p.LotNumber.Value == trimmed, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string lotNumber, CancellationToken cancellationToken = default)
    {
        var trimmed = lotNumber.Trim();
        return await _dbContext.ProductionLots.AnyAsync(p => p.LotNumber.Value == trimmed, cancellationToken);
    }

    public async Task AddAsync(ProductionLot lot, CancellationToken cancellationToken = default)
    {
        await _dbContext.ProductionLots.AddAsync(lot, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ProductionLot lot, CancellationToken cancellationToken = default)
    {
        _dbContext.ProductionLots.Update(lot);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductionLot>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProductionLots.AsNoTracking().ToListAsync(cancellationToken);
    }
}

public class MaterialLotRepository : IMaterialLotRepository
{
    private readonly TrackLordDbContext _dbContext;

    public MaterialLotRepository(TrackLordDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MaterialLot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MaterialLots.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<MaterialLot?> GetByLotNumberAsync(string lotNumber, CancellationToken cancellationToken = default)
    {
        var trimmed = lotNumber.Trim();
        return await _dbContext.MaterialLots.FirstOrDefaultAsync(m => m.LotNumber.Value == trimmed, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string lotNumber, CancellationToken cancellationToken = default)
    {
        var trimmed = lotNumber.Trim();
        return await _dbContext.MaterialLots.AnyAsync(m => m.LotNumber.Value == trimmed, cancellationToken);
    }

    public async Task AddAsync(MaterialLot lot, CancellationToken cancellationToken = default)
    {
        await _dbContext.MaterialLots.AddAsync(lot, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(MaterialLot lot, CancellationToken cancellationToken = default)
    {
        _dbContext.MaterialLots.Update(lot);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

public class ManufacturingMovementRepository : IManufacturingMovementRepository
{
    private readonly TrackLordDbContext _dbContext;

    public ManufacturingMovementRepository(TrackLordDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ManufacturingMovement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ManufacturingMovements.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ManufacturingMovement>> GetMovementsForLotAsync(Guid productionLotId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ManufacturingMovements
            .Where(m => m.ProductionLotId == productionLotId)
            .OrderBy(m => m.MovementDate)
            .ThenBy(m => m.CreatedAtUtc)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ManufacturingMovement movement, CancellationToken cancellationToken = default)
    {
        await _dbContext.ManufacturingMovements.AddAsync(movement, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ManufacturingMovement movement, CancellationToken cancellationToken = default)
    {
        _dbContext.ManufacturingMovements.Update(movement);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

public class ManufacturingStepRepository : IManufacturingStepRepository
{
    private readonly TrackLordDbContext _dbContext;

    public ManufacturingStepRepository(TrackLordDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ManufacturingStep?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ManufacturingSteps.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<ManufacturingStep?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var upperCode = code.Trim().ToUpperInvariant();
        return await _dbContext.ManufacturingSteps.FirstOrDefaultAsync(s => s.Code == upperCode, cancellationToken);
    }

    public async Task<IReadOnlyList<ManufacturingStep>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ManufacturingSteps.OrderBy(s => s.SequenceOrder).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ManufacturingStep step, CancellationToken cancellationToken = default)
    {
        await _dbContext.ManufacturingSteps.AddAsync(step, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

public class MaterialConsumptionRepository : IMaterialConsumptionRepository
{
    private readonly TrackLordDbContext _dbContext;

    public MaterialConsumptionRepository(TrackLordDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(MaterialConsumption consumption, CancellationToken cancellationToken = default)
    {
        await _dbContext.MaterialConsumptions.AddAsync(consumption, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MaterialConsumption>> GetByProductionLotIdAsync(Guid productionLotId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MaterialConsumptions
            .Where(c => c.ProductionLotId == productionLotId)
            .OrderBy(c => c.ConsumptionDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MaterialConsumption>> GetByMaterialLotIdAsync(Guid materialLotId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MaterialConsumptions
            .Where(c => c.MaterialLotId == materialLotId)
            .OrderBy(c => c.ConsumptionDate)
            .ToListAsync(cancellationToken);
    }
}

public class RejectionRecordRepository : IRejectionRecordRepository
{
    private readonly TrackLordDbContext _dbContext;

    public RejectionRecordRepository(TrackLordDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(RejectionRecord rejection, CancellationToken cancellationToken = default)
    {
        await _dbContext.RejectionRecords.AddAsync(rejection, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RejectionRecord>> GetByProductionLotIdAsync(Guid productionLotId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RejectionRecords
            .Where(r => r.ProductionLotId == productionLotId)
            .OrderBy(r => r.RejectionDate)
            .ToListAsync(cancellationToken);
    }
}
