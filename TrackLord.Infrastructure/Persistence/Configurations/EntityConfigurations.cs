using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackLord.Domain.Aggregates.ClientAggregate;
using TrackLord.Domain.Aggregates.ConsumptionAggregate;
using TrackLord.Domain.Aggregates.MaterialLotAggregate;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Aggregates.UserAggregate;
using TrackLord.Domain.Entities;
using TrackLord.Domain.ValueObjects;

namespace TrackLord.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(t => t.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(t => t.SettingsJson).HasColumnName("settings_json").HasDefaultValue("{}").IsRequired();

        builder.HasIndex(t => t.Code).IsUnique();
    }
}

public class ProductionLotConfiguration : IEntityTypeConfiguration<ProductionLot>
{
    public void Configure(EntityTypeBuilder<ProductionLot> builder)
    {
        builder.ToTable("production_lots");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(p => p.LotNumber)
            .HasConversion(p => p.Value, v => new ProductionLotNumber(v))
            .HasColumnName("lot_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(p => new { p.TenantId, p.LotNumber }).IsUnique();

        builder.Property(p => p.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(p => p.InitialStepId).HasColumnName("initial_step_id").IsRequired();
        builder.Property(p => p.Status).HasColumnName("status").IsRequired();
        builder.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(p => p.Remarks).HasColumnName("remarks").HasMaxLength(500);
        builder.Property(p => p.ClientId).HasColumnName("client_id");
        builder.Property(p => p.ClientName).HasColumnName("client_name").HasMaxLength(200);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(p => p.ClientId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.OwnsOne(p => p.InitialQuantity, q =>
        {
            q.Property(x => x.Value).HasColumnName("initial_quantity").HasPrecision(18, 4).IsRequired();
            q.Property(x => x.Unit).HasColumnName("unit").IsRequired();
        });
    }
}

public class MaterialLotConfiguration : IEntityTypeConfiguration<MaterialLot>
{
    public void Configure(EntityTypeBuilder<MaterialLot> builder)
    {
        builder.ToTable("material_lots");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(m => m.LotNumber)
            .HasConversion(m => m.Value, v => new MaterialLotNumber(v))
            .HasColumnName("lot_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(m => new { m.TenantId, m.LotNumber }).IsUnique();

        builder.Property(m => m.MaterialItemId).HasColumnName("material_item_id").IsRequired();
        builder.Property(m => m.MaterialName).HasColumnName("material_name").HasMaxLength(200);
        builder.Property(m => m.UnitRate).HasColumnName("unit_rate").HasPrecision(18, 2).IsRequired().HasDefaultValue(0m);
        builder.Property(m => m.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.OwnsOne(m => m.InitialQuantity, q =>
        {
            q.Property(x => x.Value).HasColumnName("initial_quantity").HasPrecision(18, 4).IsRequired();
            q.Property(x => x.Unit).HasColumnName("unit").IsRequired();
        });

        builder.OwnsOne(m => m.CurrentQuantity, q =>
        {
            q.Property(x => x.Value).HasColumnName("current_quantity").HasPrecision(18, 4).IsRequired();
            q.Property(x => x.Unit).HasColumnName("current_unit").IsRequired();
        });
    }
}

public class ManufacturingMovementConfiguration : IEntityTypeConfiguration<ManufacturingMovement>
{
    public void Configure(EntityTypeBuilder<ManufacturingMovement> builder)
    {
        builder.ToTable("manufacturing_movements");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(m => m.ProductionLotId).HasColumnName("production_lot_id").IsRequired();
        builder.Property(m => m.FromStepId).HasColumnName("from_step_id");
        builder.Property(m => m.ToStepId).HasColumnName("to_step_id").IsRequired();
        builder.Property(m => m.Quantity).HasColumnName("quantity").HasPrecision(18, 4).IsRequired();
        builder.Property(m => m.Type).HasColumnName("type").IsRequired();
        builder.Property(m => m.MovementDate).HasColumnName("movement_date").IsRequired();
        builder.Property(m => m.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(m => m.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.Property(m => m.ClientId).HasColumnName("client_id");
        builder.Property(m => m.ClientName).HasColumnName("client_name").HasMaxLength(150);
        builder.Property(m => m.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(m => m.Remarks).HasColumnName("remarks").HasMaxLength(500);
        builder.Property(m => m.ReferenceNumber).HasColumnName("reference_number").HasMaxLength(100);
        builder.Property(m => m.IsReversed).HasColumnName("is_reversed").IsRequired();
        builder.Property(m => m.ReversalMovementId).HasColumnName("reversal_movement_id");
        builder.Property(m => m.ReversalReason).HasColumnName("reversal_reason").HasMaxLength(500);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(m => m.ClientId)
            .OnDelete(DeleteBehavior.SetNull);

        // Crucial index for Ledger Replay Engine deterministic sorting
        builder.HasIndex(m => new { m.TenantId, m.ProductionLotId, m.MovementDate, m.CreatedAtUtc, m.Id })
            .HasDatabaseName("ix_movements_lot_replay_order");
    }
}

public class ManufacturingStepConfiguration : IEntityTypeConfiguration<ManufacturingStep>
{
    public void Configure(EntityTypeBuilder<ManufacturingStep> builder)
    {
        builder.ToTable("manufacturing_steps");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(s => s.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(s => s.SequenceOrder).HasColumnName("sequence_order").IsRequired();
        builder.Property(s => s.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(s => s.IsTerminating).HasColumnName("is_terminating").IsRequired().HasDefaultValue(false);
        builder.Property(s => s.FactoryId).HasColumnName("factory_id").IsRequired();

        builder.HasIndex(s => new { s.TenantId, s.Code }).IsUnique();
    }
}

public class MaterialConsumptionConfiguration : IEntityTypeConfiguration<MaterialConsumption>
{
    public void Configure(EntityTypeBuilder<MaterialConsumption> builder)
    {
        builder.ToTable("material_consumptions");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.ProductionLotId).HasColumnName("production_lot_id").IsRequired();
        builder.Property(c => c.MaterialLotId).HasColumnName("material_lot_id").IsRequired();
        builder.Property(c => c.StepId).HasColumnName("step_id").IsRequired();
        builder.Property(c => c.QuantityConsumed).HasColumnName("quantity_consumed").HasPrecision(18, 4).IsRequired();
        builder.Property(c => c.Unit).HasColumnName("unit").IsRequired();
        builder.Property(c => c.UnitRate).HasColumnName("unit_rate").HasPrecision(18, 2).IsRequired().HasDefaultValue(0m);
        builder.Property(c => c.ConsumptionDate).HasColumnName("consumption_date").IsRequired();
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(c => c.Remarks).HasColumnName("remarks").HasMaxLength(500);

        builder.HasIndex(c => c.ProductionLotId);
        builder.HasIndex(c => c.MaterialLotId);
    }
}

public class RejectionRecordConfiguration : IEntityTypeConfiguration<RejectionRecord>
{
    public void Configure(EntityTypeBuilder<RejectionRecord> builder)
    {
        builder.ToTable("rejection_records");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(r => r.ProductionLotId).HasColumnName("production_lot_id").IsRequired();
        builder.Property(r => r.StepId).HasColumnName("step_id").IsRequired();
        builder.Property(r => r.TargetStepId).HasColumnName("target_step_id");
        builder.Property(r => r.QuantityRejected).HasColumnName("quantity_rejected").HasPrecision(18, 4).IsRequired();
        builder.Property(r => r.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(r => r.RejectionDate).HasColumnName("rejection_date").IsRequired();
        builder.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(r => r.ClientId).HasColumnName("client_id");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(100);

        builder.HasIndex(r => r.ProductionLotId);
    }
}

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(c => c.PhoneNumber).HasColumnName("phone_number").HasMaxLength(50).IsRequired();
        builder.Property(c => c.ClientType).HasColumnName("client_type").HasMaxLength(50).IsRequired();
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(c => new { c.TenantId, c.Name });
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");

        builder.Property(u => u.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(u => u.PhoneNumber).HasColumnName("phone_number").HasMaxLength(50).IsRequired();
        builder.Property(u => u.Name).HasColumnName("name").HasMaxLength(150);
        builder.Property(u => u.Role).HasColumnName("role").HasMaxLength(50).IsRequired().HasDefaultValue("Operator");
        builder.Property(u => u.FirebaseUid).HasColumnName("firebase_uid").HasMaxLength(128);
        builder.Property(u => u.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(u => u.LastLoginAtUtc).HasColumnName("last_login_at_utc");
        builder.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(u => new { u.TenantId, u.PhoneNumber }).IsUnique();
        builder.HasIndex(u => u.FirebaseUid);
    }
}


