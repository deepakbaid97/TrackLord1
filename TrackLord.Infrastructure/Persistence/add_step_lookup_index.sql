-- =============================================================================
-- Migration: Add Step Movement Lookup Index
-- Purpose: Accelerates step-based candidate lot queries for the step lots inspection feature
-- Safe to run on existing production database (IF NOT EXISTS prevents errors)
-- =============================================================================

CREATE INDEX IF NOT EXISTS ix_movements_to_step_lookup
    ON manufacturing_movements (tenant_id, to_step_id, movement_date);
