START TRANSACTION;
ALTER TABLE users RENAME COLUMN "Id" TO id;

ALTER TABLE tenants RENAME COLUMN "Id" TO id;

ALTER TABLE rejection_records RENAME COLUMN "Id" TO id;

ALTER TABLE production_lots RENAME COLUMN "Id" TO id;

ALTER TABLE material_lots RENAME COLUMN "Id" TO id;

ALTER TABLE material_consumptions RENAME COLUMN "Id" TO id;

ALTER TABLE manufacturing_steps RENAME COLUMN "Id" TO id;

ALTER TABLE manufacturing_movements RENAME COLUMN "Id" TO id;

ALTER TABLE clients RENAME COLUMN "Id" TO id;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260810131130_FixIdColumnNamingConvention', '9.0.2');

COMMIT;

