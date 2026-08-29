using System;
using System.Collections.Generic;

namespace TrackLord.Application.Queries;

public record ClientDto(
    Guid Id,
    string Name,
    string PhoneNumber,
    string ClientType,
    DateTime CreatedAtUtc);

public record StepWipDto(Guid StepId, string StepCode, string StepName, decimal Quantity);

public record WipStatusDto(
    Guid LotId,
    string LotNumber,
    string Status,
    decimal TotalInitialQuantity,
    decimal TotalCurrentWipQuantity,
    decimal TotalTerminatedQuantity,
    IReadOnlyList<StepWipDto> CurrentStepBalances);

public record MovementTimelineDto(
    Guid MovementId,
    string? FromStepName,
    string ToStepName,
    decimal Quantity,
    string MovementType,
    DateTime MovementDate,
    DateTime CreatedAtUtc,
    Guid? ClientId,
    string? ClientName,
    string? CreatedBy,
    string? Remarks,
    string? ReferenceNumber,
    bool IsReversed);

public record LotJourneyDto(
    Guid LotId,
    string LotNumber,
    string Status,
    DateTime CreatedAtUtc,
    IReadOnlyList<MovementTimelineDto> MovementHistory,
    IReadOnlyList<StepWipDto> ActiveBalances,
    Guid? ClientId = null,
    string? ClientName = null);

public record MaterialConsumptionTraceDto(
    Guid Id,
    Guid MaterialLotId,
    string MaterialLotNumber,
    string? MaterialName,
    Guid StepId,
    string StepName,
    decimal QuantityConsumed,
    string Unit,
    decimal UnitRate,
    decimal TotalPrice,
    DateTime ConsumptionDate,
    string? Remarks);

public record MaterialTraceabilityDto(
    Guid ProductionLotId,
    string ProductionLotNumber,
    decimal TotalMaterialCost,
    IReadOnlyList<MaterialConsumptionTraceDto> Consumptions);

public record MaterialConsumptionHistoryItemDto(
    Guid Id,
    Guid ProductionLotId,
    string ProductionLotNumber,
    Guid StepId,
    string StepName,
    decimal QuantityConsumed,
    string Unit,
    decimal UnitRate,
    decimal TotalPrice,
    DateTime ConsumptionDate,
    DateTime CreatedAtUtc,
    string? CreatedBy,
    string? Remarks);

public record MaterialLotDto(
    Guid Id,
    string LotNumber,
    Guid MaterialItemId,
    string? MaterialName,
    decimal InitialQuantity,
    decimal RemainingQuantity,
    string Unit,
    decimal UnitRate);

public record MaterialLotHistoryDto(
    MaterialLotDto Lot,
    decimal TotalQuantityConsumed,
    decimal TotalConsumptionCost,
    int TotalConsumptionCount,
    IReadOnlyList<MaterialConsumptionHistoryItemDto> Consumptions);

public record StepDashboardLotDto(
    Guid LotId,
    string LotNumber,
    decimal CurrentQuantityAtStep);

public record StepDashboardDto(
    Guid StepId,
    string StepCode,
    string StepName,
    decimal TotalQuantityAtStep,
    int ActiveLotsCount,
    IReadOnlyList<StepDashboardLotDto> LotsAtStep);

public record FactoryStepSummaryDto(
    Guid StepId,
    string StepCode,
    string StepName,
    decimal TotalWipQuantity);

public record FactoryDashboardDto(
    Guid FactoryId,
    decimal TotalFactoryWip,
    IReadOnlyList<FactoryStepSummaryDto> Steps);

public record ProductionLotSummaryDto(
    Guid Id,
    string LotNumber,
    decimal InitialQuantity,
    string Status,
    DateTime CreatedAtUtc,
    Guid? ClientId = null,
    string? ClientName = null);

public record ManufacturingStepDto(
    Guid Id,
    string Code,
    string Name,
    int SequenceOrder,
    bool IsTerminating,
    Guid FactoryId,
    bool IsActive = true);

public record DashboardStepSummaryDto(
    Guid StepId,
    string StepCode,
    string StepName,
    decimal TotalWipQuantity,
    int ActiveLotsCount,
    bool IsTerminating,
    bool IsActive = true);

public record DashboardClientSummaryDto(
    Guid ClientId,
    string ClientName,
    string ClientType,
    int TotalLotsAssociated,
    decimal TotalQuantityMoved);

public record DashboardActiveLotDto(
    Guid LotId,
    string LotNumber,
    decimal InitialQuantity,
    decimal CurrentWipQuantity,
    string CurrentStepName,
    DateTime CreatedAtUtc,
    Guid? ClientId = null,
    string? ClientName = null);

public record DashboardOverviewDto(
    int TotalActiveLots,
    decimal TotalActiveQuantity,
    decimal TotalTerminatedQuantity,
    int TotalClients,
    int TotalMaterialLots,
    IReadOnlyList<DashboardStepSummaryDto> StepSummaries,
    IReadOnlyList<DashboardClientSummaryDto> ClientSummaries,
    IReadOnlyList<DashboardActiveLotDto> ActiveLotSummaries);

public record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    bool IsActive,
    string PermissionsJson,
    Dictionary<string, List<string>> Permissions,
    DateTime CreatedAtUtc);

