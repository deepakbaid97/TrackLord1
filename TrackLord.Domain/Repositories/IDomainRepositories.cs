using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrackLord.Domain.Aggregates.ClientAggregate;
using TrackLord.Domain.Aggregates.ConsumptionAggregate;
using TrackLord.Domain.Aggregates.MaterialLotAggregate;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Aggregates.RoleAggregate;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Aggregates.UserAggregate;
using TrackLord.Domain.Entities;
using TrackLord.Domain.Enums;

namespace TrackLord.Domain.Repositories;

public interface IProductionLotRepository
{
    Task<ProductionLot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductionLot>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<ProductionLot?> GetByLotNumberAsync(string lotNumber, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string lotNumber, CancellationToken cancellationToken = default);
    Task AddAsync(ProductionLot lot, CancellationToken cancellationToken = default);
    Task UpdateAsync(ProductionLot lot, CancellationToken cancellationToken = default);
    Task DeleteAsync(ProductionLot lot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductionLot>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductionLot>> GetByStatusAsync(LotStatus status, CancellationToken cancellationToken = default);
}

public interface IMaterialLotRepository
{
    Task<MaterialLot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MaterialLot?> GetByLotNumberAsync(string lotNumber, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string lotNumber, CancellationToken cancellationToken = default);
    Task AddAsync(MaterialLot lot, CancellationToken cancellationToken = default);
    Task UpdateAsync(MaterialLot lot, CancellationToken cancellationToken = default);
    Task DeleteAsync(MaterialLot lot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaterialLot>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}

public interface IManufacturingMovementRepository
{
    Task<ManufacturingMovement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManufacturingMovement>> GetMovementsForLotAsync(Guid productionLotId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManufacturingMovement>> GetMovementsForLotsAsync(IEnumerable<Guid> productionLotIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetCandidateLotIdsForStepAsync(Guid stepId, DateTime? maxMovementDate = null, DateTime? minMovementDate = null, CancellationToken cancellationToken = default);
    Task AddAsync(ManufacturingMovement movement, CancellationToken cancellationToken = default);
    Task UpdateAsync(ManufacturingMovement movement, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IEnumerable<ManufacturingMovement> movements, CancellationToken cancellationToken = default);
}

public interface IManufacturingStepRepository
{
    Task<ManufacturingStep?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ManufacturingStep?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManufacturingStep>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ManufacturingStep step, CancellationToken cancellationToken = default);
    Task UpdateAsync(ManufacturingStep step, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IEnumerable<ManufacturingStep> steps, CancellationToken cancellationToken = default);
}

public interface IMaterialConsumptionRepository
{
    Task<MaterialConsumption?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(MaterialConsumption consumption, CancellationToken cancellationToken = default);
    Task UpdateAsync(MaterialConsumption consumption, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IEnumerable<MaterialConsumption> consumptions, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaterialConsumption>> GetByProductionLotIdAsync(Guid productionLotId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaterialConsumption>> GetByMaterialLotIdAsync(Guid materialLotId, CancellationToken cancellationToken = default);
}

public interface IRejectionRecordRepository
{
    Task AddAsync(RejectionRecord rejection, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IEnumerable<RejectionRecord> rejections, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RejectionRecord>> GetByProductionLotIdAsync(Guid productionLotId, CancellationToken cancellationToken = default);
}

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Client client, CancellationToken cancellationToken = default);
    Task UpdateAsync(Client client, CancellationToken cancellationToken = default);
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<User?> GetByFirebaseUidAsync(string firebaseUid, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
}

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tenant?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);
}

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Role role, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Role> roles, CancellationToken cancellationToken = default);
    Task UpdateAsync(Role role, CancellationToken cancellationToken = default);
    Task DeleteAsync(Role role, CancellationToken cancellationToken = default);
    Task<bool> HasUsersAssignedAsync(Guid roleId, CancellationToken cancellationToken = default);
}
