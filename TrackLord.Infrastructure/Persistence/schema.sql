-- =============================================================================
-- PostgreSQL Database Schema for Lot-Centric Garment Manufacturing ERP
-- System Philosophy: Immutable Production Lots & Accounting Ledger Movements
-- =============================================================================

CREATE TABLE IF NOT EXISTS manufacturing_steps (
    id UUID PRIMARY KEY,
    code VARCHAR(20) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    sequence_order INT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    factory_id UUID NOT NULL
);

CREATE TABLE IF NOT EXISTS production_lots (
    id UUID PRIMARY KEY,
    lot_number VARCHAR(50) NOT NULL UNIQUE, -- IMMUTABLE PRODUCTION LOT NUMBER
    item_id UUID NOT NULL,
    initial_quantity NUMERIC(18, 4) NOT NULL,
    unit INT NOT NULL,
    initial_step_id UUID NOT NULL REFERENCES manufacturing_steps(id),
    status INT NOT NULL DEFAULT 1, -- Active = 1, Completed = 2, Cancelled = 3
    created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    created_by VARCHAR(100),
    remarks VARCHAR(500)
);

CREATE TABLE IF NOT EXISTS material_lots (
    id UUID PRIMARY KEY,
    lot_number VARCHAR(50) NOT NULL UNIQUE, -- IMMUTABLE MATERIAL LOT NUMBER
    material_item_id UUID NOT NULL,
    initial_quantity NUMERIC(18, 4) NOT NULL,
    unit INT NOT NULL,
    current_quantity NUMERIC(18, 4) NOT NULL,
    current_unit INT NOT NULL,
    created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL
);

CREATE TABLE IF NOT EXISTS manufacturing_movements (
    id UUID PRIMARY KEY,
    production_lot_id UUID NOT NULL REFERENCES production_lots(id),
    from_step_id UUID REFERENCES manufacturing_steps(id), -- NULL for initial creation movement
    to_step_id UUID NOT NULL REFERENCES manufacturing_steps(id),
    quantity NUMERIC(18, 4) NOT NULL CHECK (quantity > 0),
    type INT NOT NULL, -- InitialProduction = 1, StepTransfer = 2, Rejection = 3, Wastage = 4, Adjustment = 5, Reversal = 6
    movement_date TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    created_by VARCHAR(100),
    remarks VARCHAR(500),
    reference_number VARCHAR(100),
    is_reversed BOOLEAN NOT NULL DEFAULT FALSE,
    reversal_movement_id UUID REFERENCES manufacturing_movements(id),
    reversal_reason VARCHAR(500)
);

CREATE TABLE IF NOT EXISTS material_consumptions (
    id UUID PRIMARY KEY,
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
    production_lot_id UUID NOT NULL REFERENCES production_lots(id),
    step_id UUID NOT NULL REFERENCES manufacturing_steps(id),
    quantity_rejected NUMERIC(18, 4) NOT NULL CHECK (quantity_rejected > 0),
    reason VARCHAR(500) NOT NULL,
    rejection_date TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    created_by VARCHAR(100)
);

-- Crucial Performance Indexes for Deterministic Replay Engine
CREATE INDEX IF NOT EXISTS ix_movements_lot_replay_order 
    ON manufacturing_movements (production_lot_id, movement_date ASC, created_at_utc ASC, id ASC);

CREATE INDEX IF NOT EXISTS ix_material_consumptions_prod_lot 
    ON material_consumptions (production_lot_id);

CREATE INDEX IF NOT EXISTS ix_material_consumptions_mat_lot 
    ON material_consumptions (material_lot_id);

CREATE INDEX IF NOT EXISTS ix_rejection_records_prod_lot 
    ON rejection_records (production_lot_id);

CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY,
    phone_number VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(150),
    role VARCHAR(50) NOT NULL DEFAULT 'Operator',
    firebase_uid VARCHAR(128),
    created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    last_login_at_utc TIMESTAMP WITH TIME ZONE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE INDEX IF NOT EXISTS ix_users_phone_number ON users (phone_number);
CREATE INDEX IF NOT EXISTS ix_users_firebase_uid ON users (firebase_uid);
