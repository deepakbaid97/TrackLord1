using System;
using System.Collections.Generic;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Exceptions;

namespace TrackLord.Domain.Services;

public record StepBalanceDto(Guid StepId, decimal Quantity);

public record LotLedgerSnapshot(
    Guid ProductionLotId,
    IReadOnlyDictionary<Guid, decimal> StepBalances,
    decimal TotalActiveQuantity,
    decimal TotalTerminatedQuantity,
    DateTime CalculatedAsOfUtc,
    int TotalMovementsReplayed);

public interface ILedgerReplayEngine
{
    /// <summary>
    /// Replays historical movements for a production lot deterministically according to:
    /// 1. MovementDate ASC
    /// 2. CreatedAtUtc ASC
    /// 3. Id ASC
    /// Enforces non-negative balance invariant across all step locations at every point in history.
    /// Steps whose IDs appear in terminatingStepIds will have their quantities counted as
    /// TotalTerminatedQuantity instead of TotalActiveQuantity.
    /// </summary>
    LotLedgerSnapshot ReplayLedger(Guid productionLotId, IEnumerable<ManufacturingMovement> movements, IReadOnlySet<Guid>? terminatingStepIds = null, DateTime? asOfDate = null);
}

public interface IUnitConverterService
{
    decimal Convert(decimal amount, UnitType fromUnit, UnitType toUnit);
}

public class UnitConverterService : IUnitConverterService
{
    public decimal Convert(decimal amount, UnitType fromUnit, UnitType toUnit)
    {
        if (fromUnit == toUnit) return amount;

        return (fromUnit, toUnit) switch
        {
            (UnitType.Pieces, UnitType.Dozens) => amount / 12m,
            (UnitType.Dozens, UnitType.Pieces) => amount * 12m,
            (UnitType.Meters, UnitType.Yards) => amount * 1.09361m,
            (UnitType.Yards, UnitType.Meters) => amount / 1.09361m,
            (UnitType.Kilograms, UnitType.Grams) => amount * 1000m,
            (UnitType.Grams, UnitType.Kilograms) => amount / 1000m,
            _ => throw new DomainException($"Explicit conversion between unit {fromUnit} and {toUnit} is not defined.")
        };
    }
}
