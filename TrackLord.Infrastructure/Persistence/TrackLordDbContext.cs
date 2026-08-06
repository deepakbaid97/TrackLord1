using Microsoft.EntityFrameworkCore;
using TrackLord.Domain.Aggregates.ConsumptionAggregate;
using TrackLord.Domain.Aggregates.MaterialLotAggregate;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Entities;

namespace TrackLord.Infrastructure.Persistence;

public class TrackLordDbContext : DbContext
{
    public TrackLordDbContext(DbContextOptions<TrackLordDbContext> options) : base(options)
    {
    }

    public DbSet<ProductionLot> ProductionLots => Set<ProductionLot>();
    public DbSet<MaterialLot> MaterialLots => Set<MaterialLot>();
    public DbSet<ManufacturingMovement> ManufacturingMovements => Set<ManufacturingMovement>();
    public DbSet<ManufacturingStep> ManufacturingSteps => Set<ManufacturingStep>();
    public DbSet<MaterialConsumption> MaterialConsumptions => Set<MaterialConsumption>();
    public DbSet<RejectionRecord> RejectionRecords => Set<RejectionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrackLordDbContext).Assembly);
    }
}
