using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrackLord.Domain.Aggregates.ConsumptionAggregate;
using TrackLord.Domain.Aggregates.MaterialLotAggregate;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Entities;

namespace TrackLord.Domain.Repositories;

public interface IProductionLotRepository
{
    Task<ProductionLot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductionLot?> GetByLotNumberAsync(string lotNumber, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string lotNumber, CancellationToken cancellationToken = default);
    Task AddAsync(ProductionLot lot, CancellationToken cancellationToken = default);
    Task UpdateAsync(ProductionLot lot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductionLot>> GetAllAsync(CancellationToken cancellationToken = default);
}

public interface IMaterialLotRepository
{
    Task<MaterialLot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MaterialLot?> GetByLotNumberAsync(string lotNumber, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string lotNumber, CancellationToken cancellationToken = default);
    Task AddAsync(MaterialLot lot, CancellationToken cancellationToken = default);
    Task UpdateAsync(MaterialLot lot, CancellationToken cancellationToken = default);
}

public interface IManufacturingMovementRepository
{
    Task<ManufacturingMovement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManufacturingMovement>> GetMovementsForLotAsync(Guid productionLotId, CancellationToken cancellationToken = default);
    Task AddAsync(ManufacturingMovement movement, CancellationToken cancellationToken = default);
    Task UpdateAsync(ManufacturingMovement movement, CancellationToken cancellationToken = default);
}

public interface IManufacturingStepRepository
{
    Task<ManufacturingStep?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ManufacturingStep?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManufacturingStep>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ManufacturingStep step, CancellationToken cancellationToken = default);
}

public interface IMaterialConsumptionRepository
{
    Task AddAsync(MaterialConsumption consumption, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaterialConsumption>> GetByProductionLotIdAsync(Guid productionLotId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaterialConsumption>> GetByMaterialLotIdAsync(Guid materialLotId, CancellationToken cancellationToken = default);
}

public interface IRejectionRecordRepository
{
    Task AddAsync(RejectionRecord rejection, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RejectionRecord>> GetByProductionLotIdAsync(Guid productionLotId, CancellationToken cancellationToken = default);
}
