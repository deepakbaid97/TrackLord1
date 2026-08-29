-- =============================================================================
-- PostgreSQL Script: Create New Tenant with Default Configuration & Steps
-- Database: PostgreSQL (TrackLord Lot-Centric Garment Manufacturing ERP)
-- Usage: Edit the parameters in the DO block below and execute in psql / DBeaver / pgAdmin.
-- =============================================================================

DO $$
DECLARE
    -- -------------------------------------------------------------------------
    -- EDIT CONFIGURATION PARAMETERS HERE
    -- -------------------------------------------------------------------------
    v_tenant_name    VARCHAR(150) := 'Aliya Wash';   -- Name of the Organization / Company
    v_tenant_code    VARCHAR(50)  := 'ALIYA';                -- Unique Tenant Code / Short Identifier
    v_admin_name     VARCHAR(150) := 'Jitendra Bothra';  -- Primary Admin User Name
    v_admin_phone    VARCHAR(50)  := '+919876543210';       -- Primary Admin Mobile Number with Country Code
    v_admin_role     VARCHAR(50)  := 'Admin';               -- Primary User Role ('Admin', 'Manager', 'Operator')
    -- -------------------------------------------------------------------------

    v_tenant_id      UUID := gen_random_uuid();
    v_factory_id     UUID := gen_random_uuid();
    v_admin_user_id  UUID := gen_random_uuid();
    v_admin_role_id  UUID := gen_random_uuid();
    v_now            TIMESTAMP WITH TIME ZONE := NOW();
BEGIN
    v_tenant_code := UPPER(TRIM(v_tenant_code));

    -- 1. Check if tenant code already exists
    IF EXISTS (SELECT 1 FROM tenants WHERE code = v_tenant_code) THEN
        RAISE EXCEPTION 'Tenant with code "%" already exists in the database!', v_tenant_code;
    END IF;

    RAISE NOTICE '------------------------------------------------------------';
    RAISE NOTICE 'Creating Tenant: % (Code: %)', v_tenant_name, v_tenant_code;
    RAISE NOTICE 'Assigned Tenant UUID: %', v_tenant_id;
    RAISE NOTICE '------------------------------------------------------------';

    -- 2. Insert Tenant Record
    INSERT INTO tenants (id, name, code, created_at_utc, is_active)
    VALUES (v_tenant_id, TRIM(v_tenant_name), v_tenant_code, v_now, TRUE);

    -- 3. Insert Default System Roles for Tenant
    INSERT INTO roles (id, tenant_id, name, description, is_system_role, is_active, permissions_json, created_at_utc)
    VALUES 
        (v_admin_role_id, v_tenant_id, 'Admin', 'Unrestricted administrator access across all features', TRUE, TRUE, '{"production_lots":["read","write","delete"],"movements":["read","write"],"movement_reversals":["write"],"material_lots":["read","write","delete"],"material_consumptions":["read","write"],"rejection_records":["read","write"],"manufacturing_steps":["read","write"],"clients":["read","write"],"users":["read","write"],"roles":["read","write"],"tenant_settings":["read","write"],"dashboard":["read"],"imports":["write"]}', v_now),
        (gen_random_uuid(), v_tenant_id, 'Supervisor', 'Production oversight, step configuration, and movement reversals', TRUE, TRUE, '{"production_lots":["read","write"],"movements":["read","write"],"movement_reversals":["write"],"material_lots":["read","write"],"material_consumptions":["read","write"],"rejection_records":["read","write"],"manufacturing_steps":["read","write"],"clients":["read","write"],"users":["read"],"dashboard":["read"],"imports":["write"]}', v_now),
        (gen_random_uuid(), v_tenant_id, 'Manager', 'Plant manager with team and operational oversight', TRUE, TRUE, '{"production_lots":["read","write","delete"],"movements":["read","write"],"movement_reversals":["write"],"material_lots":["read","write"],"material_consumptions":["read","write"],"rejection_records":["read","write"],"manufacturing_steps":["read","write"],"clients":["read","write"],"users":["read","write"],"roles":["read"],"dashboard":["read"],"imports":["write"]}', v_now),
        (gen_random_uuid(), v_tenant_id, 'Operator', 'Factory floor worker for movements, material logs, and defect reporting', TRUE, TRUE, '{"production_lots":["read"],"movements":["read","write"],"manufacturing_steps":["read"],"clients":["read"],"material_lots":["read"],"material_consumptions":["read","write"],"rejection_records":["read","write"],"dashboard":["read"]}', v_now);

    -- 4. Insert Initial Manufacturing Steps for Tenant
    INSERT INTO manufacturing_steps (id, tenant_id, code, name, sequence_order, is_active, is_terminating, factory_id)
    VALUES 
        (gen_random_uuid(), v_tenant_id, 'CUT',    'Cutting',   1, TRUE, FALSE, v_factory_id),
        (gen_random_uuid(), v_tenant_id, 'STITCH', 'Stitching', 2, TRUE, FALSE, v_factory_id),
        (gen_random_uuid(), v_tenant_id, 'WASH',   'Washing',   3, TRUE, FALSE, v_factory_id),
        (gen_random_uuid(), v_tenant_id, 'FIN',    'Finishing', 4, TRUE, FALSE, v_factory_id),
        (gen_random_uuid(), v_tenant_id, 'PACK',   'Packing',   5, TRUE, TRUE,  v_factory_id);

    -- 5. Insert Primary Admin User for Tenant
    INSERT INTO users (id, tenant_id, phone_number, name, role, role_id, created_at_utc, is_active)
    VALUES (v_admin_user_id, v_tenant_id, TRIM(v_admin_phone), TRIM(v_admin_name), TRIM(v_admin_role), v_admin_role_id, v_now, TRUE)
    ON CONFLICT (tenant_id, phone_number) DO UPDATE 
        SET name = EXCLUDED.name, role = EXCLUDED.role, role_id = EXCLUDED.role_id, is_active = TRUE;

    RAISE NOTICE 'SUCCESS: Tenant "%" created successfully.', v_tenant_name;
    RAISE NOTICE '  - Tenant ID  : %', v_tenant_id;
    RAISE NOTICE '  - Tenant Code: %', v_tenant_code;
    RAISE NOTICE '  - Admin User : % (%)', v_admin_name, v_admin_phone;
    RAISE NOTICE '  - System Roles Created: Admin, Supervisor, Manager, Operator';
    RAISE NOTICE '  - Initial Steps Created: CUT, STITCH, WASH, FIN, PACK';
    RAISE NOTICE '------------------------------------------------------------';

EXCEPTION
    WHEN OTHERS THEN
        RAISE EXCEPTION 'Failed to create tenant: %', SQLERRM;
END $$;

