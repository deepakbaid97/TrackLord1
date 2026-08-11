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

    -- 3. Insert Initial Manufacturing Steps for Tenant
    INSERT INTO manufacturing_steps (id, tenant_id, code, name, sequence_order, is_active, is_terminating, factory_id)
    VALUES 
        (gen_random_uuid(), v_tenant_id, 'CUT',    'Cutting',   1, TRUE, FALSE, v_factory_id),
        (gen_random_uuid(), v_tenant_id, 'STITCH', 'Stitching', 2, TRUE, FALSE, v_factory_id),
        (gen_random_uuid(), v_tenant_id, 'WASH',   'Washing',   3, TRUE, FALSE, v_factory_id),
        (gen_random_uuid(), v_tenant_id, 'FIN',    'Finishing', 4, TRUE, FALSE, v_factory_id),
        (gen_random_uuid(), v_tenant_id, 'PACK',   'Packing',   5, TRUE, TRUE,  v_factory_id);

    -- 4. Insert Primary Admin User for Tenant
    INSERT INTO users (id, tenant_id, phone_number, name, role, created_at_utc, is_active)
    VALUES (v_admin_user_id, v_tenant_id, TRIM(v_admin_phone), TRIM(v_admin_name), TRIM(v_admin_role), v_now, TRUE)
    ON CONFLICT (tenant_id, phone_number) DO UPDATE 
        SET name = EXCLUDED.name, role = EXCLUDED.role, is_active = TRUE;

    RAISE NOTICE 'SUCCESS: Tenant "%" created successfully.', v_tenant_name;
    RAISE NOTICE '  - Tenant ID  : %', v_tenant_id;
    RAISE NOTICE '  - Tenant Code: %', v_tenant_code;
    RAISE NOTICE '  - Admin User : % (%)', v_admin_name, v_admin_phone;
    RAISE NOTICE '  - Initial Steps Created: CUT, STITCH, WASH, FIN, PACK';
    RAISE NOTICE '------------------------------------------------------------';

EXCEPTION
    WHEN OTHERS THEN
        RAISE EXCEPTION 'Failed to create tenant: %', SQLERRM;
END $$;
