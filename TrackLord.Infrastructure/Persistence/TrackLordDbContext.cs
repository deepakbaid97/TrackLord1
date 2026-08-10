using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TrackLord.Domain.Aggregates.ClientAggregate;
using TrackLord.Domain.Aggregates.ConsumptionAggregate;
using TrackLord.Domain.Aggregates.MaterialLotAggregate;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Aggregates.UserAggregate;
using TrackLord.Domain.Common;
using TrackLord.Domain.Entities;

namespace TrackLord.Infrastructure.Persistence;

public class TrackLordDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public TrackLordDbContext(DbContextOptions<TrackLordDbContext> options, ITenantContext? tenantContext = null) : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ProductionLot> ProductionLots => Set<ProductionLot>();
    public DbSet<MaterialLot> MaterialLots => Set<MaterialLot>();
    public DbSet<ManufacturingMovement> ManufacturingMovements => Set<ManufacturingMovement>();
    public DbSet<ManufacturingStep> ManufacturingSteps => Set<ManufacturingStep>();
    public DbSet<MaterialConsumption> MaterialConsumptions => Set<MaterialConsumption>();
    public DbSet<RejectionRecord> RejectionRecords => Set<RejectionRecord>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<User> Users => Set<User>();

    public Guid CurrentTenantId => _tenantContext?.TenantId ?? Tenant.DefaultTenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrackLordDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScopedEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(TrackLordDbContext)
                    .GetMethod(nameof(ConfigureTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    private void ConfigureTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantScopedEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PopulateTenantIds();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        PopulateTenantIds();
        return base.SaveChanges();
    }

    private void PopulateTenantIds()
    {
        if (_tenantContext == null) return;

        var activeTenantId = _tenantContext.TenantId;
        if (activeTenantId == System.Guid.Empty)
        {
            activeTenantId = Tenant.DefaultTenantId;
        }

        foreach (var entry in ChangeTracker.Entries<ITenantScopedEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                var currentTenantIdProp = entry.Property(x => x.TenantId);
                if (currentTenantIdProp.CurrentValue == System.Guid.Empty || currentTenantIdProp.CurrentValue == Tenant.DefaultTenantId)
                {
                    currentTenantIdProp.CurrentValue = activeTenantId;
                }
            }
        }
    }
}
