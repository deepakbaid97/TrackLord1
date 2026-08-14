-- =============================================================================
-- PostgreSQL Database Schema for Multi-Tenant Lot-Centric Garment Manufacturing ERP
-- System Philosophy: Multi-Tenant Isolation, Immutable Production Lots & Ledger Movements
-- =============================================================================

CREATE TABLE IF NOT EXISTS tenants (
    id UUID PRIMARY KEY,
    name VARCHAR(150) NOT NULL,
    code VARCHAR(50) NOT NULL UNIQUE,
    created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    settings_json TEXT NOT NULL DEFAULT '{}'
);

-- Seed Default Tenant if not existing
INSERT INTO tenants (id, name, code, created_at_utc, is_active, settings_json)
VALUES ('11111111-1111-1111-1111-111111111111', 'Default Tenant', 'DEFAULT', NOW(), TRUE, '{}')
ON CONFLICT (id) DO NOTHING;

CREATE TABLE IF NOT EXISTS manufacturing_steps (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    code VARCHAR(20) NOT NULL,
    name VARCHAR(100) NOT NULL,
    sequence_order INT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    is_terminating BOOLEAN NOT NULL DEFAULT FALSE,
    factory_id UUID NOT NULL,
    CONSTRAINT uq_steps_tenant_code UNIQUE (tenant_id, code)
);

CREATE TABLE IF NOT EXISTS production_lots (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    lot_number VARCHAR(50) NOT NULL, -- IMMUTABLE PRODUCTION LOT NUMBER (PER TENANT)
    item_id UUID NOT NULL,
    initial_quantity NUMERIC(18, 4) NOT NULL,
    unit INT NOT NULL,
    initial_step_id UUID NOT NULL REFERENCES manufacturing_steps(id),
    status INT NOT NULL DEFAULT 1, -- Active = 1, Completed = 2, Cancelled = 3
    created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    created_by VARCHAR(100),
    remarks VARCHAR(500),
    client_id UUID REFERENCES clients(id),
    client_name VARCHAR(200),
    CONSTRAINT uq_production_lots_tenant_lot_number UNIQUE (tenant_id, lot_number)
);

CREATE TABLE IF NOT EXISTS material_lots (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    lot_number VARCHAR(50) NOT NULL, -- IMMUTABLE MATERIAL LOT NUMBER (PER TENANT)
    material_item_id UUID NOT NULL,
    material_name VARCHAR(200),
    initial_quantity NUMERIC(18, 4) NOT NULL,
    unit INT NOT NULL,
    current_quantity NUMERIC(18, 4) NOT NULL,
    current_unit INT NOT NULL,
    created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    CONSTRAINT uq_material_lots_tenant_lot_number UNIQUE (tenant_id, lot_number)
);

CREATE TABLE IF NOT EXISTS clients (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    name VARCHAR(150) NOT NULL,
    phone_number VARCHAR(50) NOT NULL,
    client_type VARCHAR(50) NOT NULL,
    created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS manufacturing_movements (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    production_lot_id UUID NOT NULL REFERENCES production_lots(id),
    from_step_id UUID REFERENCES manufacturing_steps(id), -- NULL for initial creation movement
    to_step_id UUID NOT NULL REFERENCES manufacturing_steps(id),
    quantity NUMERIC(18, 4) NOT NULL CHECK (quantity > 0),
    type INT NOT NULL, -- InitialProduction = 1, StepTransfer = 2, Rejection = 3, Wastage = 4, Adjustment = 5, Reversal = 6
    movement_date TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    client_id UUID REFERENCES clients(id),
    client_name VARCHAR(150),
    created_by VARCHAR(100),
    remarks VARCHAR(500),
    reference_number VARCHAR(100),
    is_reversed BOOLEAN NOT NULL DEFAULT FALSE,
    reversal_movement_id UUID REFERENCES manufacturing_movements(id),
    reversal_reason VARCHAR(500)
);

CREATE TABLE IF NOT EXISTS material_consumptions (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    production_lot_id UUID NOT NULL REFERENCES production_lots(id),
    material_lot_id UUID NOT NULL REFERENCES material_lots(id),
    step_id UUID NOT NULL REFERENCES manufacturing_steps(id),
    quantity_consumed NUMERIC(18, 4) NOT NULL CHECK (quantity_consumed > 0),
    unit INT NOT NULL,
    consumption_date TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    created_by VARCHAR(100),
    remarks VARCHAR(500)
);

CREATE TABLE IF NOT EXISTS rejection_records (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    production_lot_id UUID NOT NULL REFERENCES production_lots(id),
    step_id UUID NOT NULL REFERENCES manufacturing_steps(id),
    target_step_id UUID REFERENCES manufacturing_steps(id),
    quantity_rejected NUMERIC(18, 4) NOT NULL CHECK (quantity_rejected > 0),
    reason VARCHAR(500) NOT NULL,
    rejection_date TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    client_id UUID REFERENCES clients(id),
    created_by VARCHAR(100)
);

-- Crucial Performance Indexes for Deterministic Replay Engine per Tenant
CREATE INDEX IF NOT EXISTS ix_movements_lot_replay_order 
    ON manufacturing_movements (tenant_id, production_lot_id, movement_date ASC, created_at_utc ASC, id ASC);

CREATE INDEX IF NOT EXISTS ix_material_consumptions_prod_lot 
    ON material_consumptions (tenant_id, production_lot_id);

CREATE INDEX IF NOT EXISTS ix_material_consumptions_mat_lot 
    ON material_consumptions (tenant_id, material_lot_id);

CREATE INDEX IF NOT EXISTS ix_rejection_records_prod_lot 
    ON rejection_records (tenant_id, production_lot_id);

CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    phone_number VARCHAR(50) NOT NULL,
    name VARCHAR(150),
    role VARCHAR(50) NOT NULL DEFAULT 'Operator',
    firebase_uid VARCHAR(128),
    created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    last_login_at_utc TIMESTAMP WITH TIME ZONE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT uq_users_tenant_phone UNIQUE (tenant_id, phone_number)
);

CREATE INDEX IF NOT EXISTS ix_users_phone_number ON users (phone_number);
CREATE INDEX IF NOT EXISTS ix_users_firebase_uid ON users (firebase_uid);
