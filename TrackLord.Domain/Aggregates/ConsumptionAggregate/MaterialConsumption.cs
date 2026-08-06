using System;
using TrackLord.Domain.Common;
using TrackLord.Domain.Enums;
using TrackLord.Domain.Events;
using TrackLord.Domain.Exceptions;

namespace TrackLord.Domain.Aggregates.ConsumptionAggregate;

public class MaterialConsumption : AggregateRoot<Guid>
{
    public Guid ProductionLotId { get; private set; }
    public Guid MaterialLotId { get; private set; }
    public Guid StepId { get; private set; }
    public decimal QuantityConsumed { get; private set; }
    public UnitType Unit { get; private set; }
    public DateTime ConsumptionDate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public string? Remarks { get; private set; }

    private MaterialConsumption() { } // EF Core

    public static MaterialConsumption Create(
        Guid id,
        Guid productionLotId,
        Guid materialLotId,
        Guid stepId,
        decimal quantityConsumed,
        UnitType unit,
        DateTime consumptionDate,
        string? createdBy = null,
        string? remarks = null)
    {
        if (id == Guid.Empty) throw new DomainException("Consumption Id cannot be empty.");
        if (productionLotId == Guid.Empty) throw new DomainException("ProductionLotId cannot be empty.");
        if (materialLotId == Guid.Empty) throw new DomainException("MaterialLotId cannot be empty.");
        if (stepId == Guid.Empty) throw new DomainException("StepId cannot be empty.");
        if (quantityConsumed <= 0)
        {
            throw new DomainException($"Consumed quantity must be greater than zero. Provided: {quantityConsumed}");
        }

        var utcNow = DateTime.UtcNow;
        var consumption = new MaterialConsumption
        {
            Id = id,
            ProductionLotId = productionLotId,
            MaterialLotId = materialLotId,
            StepId = stepId,
            QuantityConsumed = quantityConsumed,
            Unit = unit,
            ConsumptionDate = consumptionDate,
            CreatedAtUtc = utcNow,
            CreatedBy = createdBy,
            Remarks = remarks
        };

        consumption.AddDomainEvent(new MaterialConsumedEvent(
            consumption.Id,
            consumption.ProductionLotId,
            consumption.MaterialLotId,
            consumption.StepId,
            consumption.QuantityConsumed,
            consumption.Unit,
            consumption.ConsumptionDate,
            consumption.CreatedAtUtc,
            consumption.CreatedBy));

        return consumption;
    }

    public void Update(decimal newQuantityConsumed, string? newRemarks = null)
    {
        if (newQuantityConsumed < 0)
        {
            throw new DomainException($"Consumed quantity must be greater than zero. Provided: {newQuantityConsumed}");
        }

        QuantityConsumed = newQuantityConsumed;
        if (newRemarks is not null)
        {
            Remarks = newRemarks;
        }
    }
}
