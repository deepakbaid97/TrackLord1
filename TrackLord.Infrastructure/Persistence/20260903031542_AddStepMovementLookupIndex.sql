-- =============================================================================
-- Migration: 20260903031542_AddStepMovementLookupIndex
-- Applied to: TrackLord PostgreSQL Production / Staging Database
-- =============================================================================

START TRANSACTION;

CREATE INDEX IF NOT EXISTS ix_movements_to_step_lookup 
    ON manufacturing_movements (tenant_id, to_step_id, movement_date);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260903031542_AddStepMovementLookupIndex', '9.0.2')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
