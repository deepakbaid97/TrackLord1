using System;
using System.Collections.Generic;
using System.Linq;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Exceptions;
using TrackLord.Domain.Services;

namespace TrackLord.Infrastructure.Services;

public class LedgerReplayEngine : ILedgerReplayEngine
{
    public LotLedgerSnapshot ReplayLedger(Guid productionLotId, IEnumerable<ManufacturingMovement> movements)
    {
        if (movements is null)
        {
            throw new ArgumentNullException(nameof(movements));
        }

        // Rule 12: Movement ordering must sort by MovementDate, then CreatedAtUtc, then Id to guarantee deterministic replay
        var sortedMovements = movements
            .OrderBy(m => m.MovementDate)
            .ThenBy(m => m.CreatedAtUtc)
            .ThenBy(m => m.Id)
            .ToList();

        var stepBalances = new Dictionary<Guid, decimal>();

        foreach (var movement in sortedMovements)
        {
            // Handle deduction from source step (if applicable)
            if (movement.FromStepId.HasValue)
            {
                var fromStep = movement.FromStepId.Value;
                if (!stepBalances.TryGetValue(fromStep, out var currentFromBal))
                {
                    currentFromBal = 0m;
                }

                var newFromBal = currentFromBal - movement.Quantity;
                if (newFromBal < 0)
                {
                    throw new DomainException(
                        $"Ledger Replay Validation Failure: Production Lot '{productionLotId}' balance at Step '{fromStep}' " +
                        $"would become negative ({newFromBal}) on Movement Date '{movement.MovementDate:yyyy-MM-dd HH:mm:ss}' " +
                        $"(Movement ID: '{movement.Id}'). Current available balance was {currentFromBal}, attempted movement quantity: {movement.Quantity}.");
                }

                stepBalances[fromStep] = newFromBal;
            }

            // Handle addition to target step (or rejection/wastage handling)
            if (movement.Type == MovementType.Wastage)
            {
                // Wastage reduces inventory at FromStepId without adding to ToStepId
            }
            else if (movement.Type == MovementType.Rejection && movement.FromStepId.HasValue && movement.FromStepId.Value == movement.ToStepId)
            {
                // Legacy rejection at same step without target step reduces inventory without adding
            }
            else
            {
                var toStep = movement.ToStepId;
                if (!stepBalances.TryGetValue(toStep, out var currentToBal))
                {
                    currentToBal = 0m;
                }

                stepBalances[toStep] = currentToBal + movement.Quantity;
            }
        }

        // Filter out zero balances for clean snapshot representation
        var activeBalances = stepBalances
            .Where(kvp => kvp.Value > 0)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        var totalActive = activeBalances.Values.Sum();

        return new LotLedgerSnapshot(
            productionLotId,
            activeBalances,
            totalActive,
            DateTime.UtcNow,
            sortedMovements.Count);
    }
}
