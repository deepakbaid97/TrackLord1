-- =============================================================================
-- PostgreSQL Script: Fix Multi-Tenant System Roles & Re-align User Role IDs
-- =============================================================================
-- Problem Resolved:
--   Previously, only the default tenant ('11111111-1111-1111-1111-111111111111')
--   had roles created, causing users belonging to other tenants to mistakenly
--   point to the default tenant's role IDs.
--
-- What This Script Does:
--   1. Identifies every distinct tenant ID (from `tenants` and `users` tables).
--   2. Ensures default system roles ('Admin', 'Supervisor', 'Manager', 'Operator')
--      exist uniquely FOR EACH TENANT with `roles.tenant_id = that_tenant_id`.
--   3. Updates permissions JSON on all system roles with the latest feature keys.
--   4. Re-aligns every user in the `users` table so `users.role_id` points to
--      THEIR OWN TENANT'S role ID matching their role name.
--   5. Verifies and displays a clean audit summary.
--
-- Safe & Idempotent: Can be executed multiple times safely without errors or duplicates.
-- =============================================================================

DO $$
DECLARE
    t_rec RECORD;
    u_rec RECORD;
    v_now TIMESTAMP WITH TIME ZONE := NOW();
    v_admin_role_id      UUID;
    v_supervisor_role_id UUID;
    v_manager_role_id    UUID;
    v_operator_role_id   UUID;
    v_admin_perms        TEXT := '{"production_lots":["read","write","delete"],"movements":["read","write"],"movement_reversals":["write"],"material_lots":["read","write","delete"],"material_consumptions":["read","write"],"rejection_records":["read","write"],"manufacturing_steps":["read","write"],"clients":["read","write"],"users":["read","write"],"roles":["read","write"],"tenant_settings":["read","write"],"dashboard":["read"],"imports":["write"]}';
    v_supervisor_perms   TEXT := '{"production_lots":["read","write"],"movements":["read","write"],"movement_reversals":["write"],"material_lots":["read","write"],"material_consumptions":["read","write"],"rejection_records":["read","write"],"manufacturing_steps":["read","write"],"clients":["read","write"],"users":["read"],"dashboard":["read"],"imports":["write"]}';
    v_manager_perms      TEXT := '{"production_lots":["read","write","delete"],"movements":["read","write"],"movement_reversals":["write"],"material_lots":["read","write"],"material_consumptions":["read","write"],"rejection_records":["read","write"],"manufacturing_steps":["read","write"],"clients":["read","write"],"users":["read","write"],"roles":["read"],"dashboard":["read"],"imports":["write"]}';
    v_operator_perms     TEXT := '{"production_lots":["read"],"movements":["read","write"],"manufacturing_steps":["read"],"clients":["read"],"material_lots":["read"],"material_consumptions":["read","write"],"rejection_records":["read","write"],"dashboard":["read"]}';
    
    v_tenant_count       INT := 0;
    v_roles_created      INT := 0;
    v_users_updated      INT := 0;
    v_mismatched_count   INT := 0;
BEGIN
    RAISE NOTICE '============================================================';
    RAISE NOTICE 'Starting Per-Tenant Roles Seeding & User Role Alignment...';
    RAISE NOTICE '============================================================';

    -- 1. Ensure `role_id` column exists on `users` table
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'users' AND column_name = 'role_id'
    ) THEN
        ALTER TABLE users ADD COLUMN role_id UUID REFERENCES roles(id);
        CREATE INDEX IF NOT EXISTS ix_users_role_id ON users (role_id);
        RAISE NOTICE 'Added missing column users.role_id';
    END IF;

    -- 2. Process all distinct tenants (from `tenants` and any tenant referenced in `users`)
    FOR t_rec IN (
        SELECT id AS tenant_id, name, code FROM tenants
        UNION
        SELECT DISTINCT tenant_id, 'Tenant ' || SUBSTRING(tenant_id::text, 1, 8), 'T_' || SUBSTRING(tenant_id::text, 1, 6)
        FROM users 
        WHERE tenant_id IS NOT NULL 
          AND tenant_id NOT IN (SELECT id FROM tenants)
    ) LOOP
        v_tenant_count := v_tenant_count + 1;
        
        -- Ensure tenant exists in `tenants` table
        INSERT INTO tenants (id, name, code, created_at_utc, is_active)
        VALUES (t_rec.tenant_id, t_rec.name, t_rec.code, v_now, TRUE)
        ON CONFLICT (id) DO NOTHING;

        RAISE NOTICE '------------------------------------------------------------';
        RAISE NOTICE 'Processing Tenant [%]: "%" (ID: %)', v_tenant_count, t_rec.name, t_rec.tenant_id;

        -- A. Admin Role for THIS Tenant
        SELECT id INTO v_admin_role_id FROM roles WHERE tenant_id = t_rec.tenant_id AND name = 'Admin';
        IF v_admin_role_id IS NULL THEN
            v_admin_role_id := gen_random_uuid();
            INSERT INTO roles (id, tenant_id, name, description, is_system_role, is_active, permissions_json, created_at_utc)
            VALUES (v_admin_role_id, t_rec.tenant_id, 'Admin', 'Unrestricted administrator access across all features', TRUE, TRUE, v_admin_perms, v_now);
            v_roles_created := v_roles_created + 1;
            RAISE NOTICE '  + Created [Admin] Role: %', v_admin_role_id;
        ELSE
            UPDATE roles SET permissions_json = v_admin_perms, is_system_role = TRUE, is_active = TRUE WHERE id = v_admin_role_id;
        END IF;

        -- B. Supervisor Role for THIS Tenant
        SELECT id INTO v_supervisor_role_id FROM roles WHERE tenant_id = t_rec.tenant_id AND name = 'Supervisor';
        IF v_supervisor_role_id IS NULL THEN
            v_supervisor_role_id := gen_random_uuid();
            INSERT INTO roles (id, tenant_id, name, description, is_system_role, is_active, permissions_json, created_at_utc)
            VALUES (v_supervisor_role_id, t_rec.tenant_id, 'Supervisor', 'Production oversight, step configuration, and movement reversals', TRUE, TRUE, v_supervisor_perms, v_now);
            v_roles_created := v_roles_created + 1;
            RAISE NOTICE '  + Created [Supervisor] Role: %', v_supervisor_role_id;
        ELSE
            UPDATE roles SET permissions_json = v_supervisor_perms, is_system_role = TRUE, is_active = TRUE WHERE id = v_supervisor_role_id;
        END IF;

        -- C. Manager Role for THIS Tenant
        SELECT id INTO v_manager_role_id FROM roles WHERE tenant_id = t_rec.tenant_id AND name = 'Manager';
        IF v_manager_role_id IS NULL THEN
            v_manager_role_id := gen_random_uuid();
            INSERT INTO roles (id, tenant_id, name, description, is_system_role, is_active, permissions_json, created_at_utc)
            VALUES (v_manager_role_id, t_rec.tenant_id, 'Manager', 'Plant manager with team and operational oversight', TRUE, TRUE, v_manager_perms, v_now);
            v_roles_created := v_roles_created + 1;
            RAISE NOTICE '  + Created [Manager] Role: %', v_manager_role_id;
        ELSE
            UPDATE roles SET permissions_json = v_manager_perms, is_system_role = TRUE, is_active = TRUE WHERE id = v_manager_role_id;
        END IF;

        -- D. Operator Role for THIS Tenant
        SELECT id INTO v_operator_role_id FROM roles WHERE tenant_id = t_rec.tenant_id AND name = 'Operator';
        IF v_operator_role_id IS NULL THEN
            v_operator_role_id := gen_random_uuid();
            INSERT INTO roles (id, tenant_id, name, description, is_system_role, is_active, permissions_json, created_at_utc)
            VALUES (v_operator_role_id, t_rec.tenant_id, 'Operator', 'Factory floor worker for movements, material logs, and defect reporting', TRUE, TRUE, v_operator_perms, v_now);
            v_roles_created := v_roles_created + 1;
            RAISE NOTICE '  + Created [Operator] Role: %', v_operator_role_id;
        ELSE
            UPDATE roles SET permissions_json = v_operator_perms, is_system_role = TRUE, is_active = TRUE WHERE id = v_operator_role_id;
        END IF;

        -- E. Re-align all users of THIS tenant to point to THIS tenant's own role IDs
        UPDATE users
        SET role_id = CASE 
                WHEN LOWER(TRIM(COALESCE(role, ''))) = 'admin' THEN v_admin_role_id
                WHEN LOWER(TRIM(COALESCE(role, ''))) = 'supervisor' THEN v_supervisor_role_id
                WHEN LOWER(TRIM(COALESCE(role, ''))) = 'manager' THEN v_manager_role_id
                ELSE v_operator_role_id
            END,
            role = CASE
                WHEN LOWER(TRIM(COALESCE(role, ''))) = 'admin' THEN 'Admin'
                WHEN LOWER(TRIM(COALESCE(role, ''))) = 'supervisor' THEN 'Supervisor'
                WHEN LOWER(TRIM(COALESCE(role, ''))) = 'manager' THEN 'Manager'
                ELSE 'Operator'
            END
        WHERE tenant_id = t_rec.tenant_id;

        GET DIAGNOSTICS v_users_updated = ROW_COUNT;
        RAISE NOTICE '  * Re-aligned % user(s) to Tenant % roles.', v_users_updated, t_rec.tenant_id;

    END LOOP;

    -- 3. Audit & Verification Check
    SELECT COUNT(*) INTO v_mismatched_count
    FROM users u
    LEFT JOIN roles r ON u.role_id = r.id
    WHERE u.role_id IS NULL OR r.tenant_id != u.tenant_id;

    RAISE NOTICE '============================================================';
    RAISE NOTICE 'AUDIT & VERIFICATION REPORT';
    RAISE NOTICE '============================================================';
    RAISE NOTICE 'Total Tenants Processed   : %', v_tenant_count;
    RAISE NOTICE 'New Roles Created         : %', v_roles_created;
    RAISE NOTICE 'Mismatched / Unlinked Users: %', v_mismatched_count;

    IF v_mismatched_count = 0 THEN
        RAISE NOTICE 'SUCCESS: 100%% of users are now correctly linked to their tenant-isolated roles!';
    ELSE
        RAISE WARNING 'ATTENTION: Found % user(s) with mismatched tenant_id / role_id.', v_mismatched_count;
    END IF;
    RAISE NOTICE '============================================================';

END $$;

-- 4. Print Quick Visual Table to Verify in Database Client (psql / pgAdmin / DBeaver)
SELECT 
    u.id AS user_id,
    u.name AS user_name,
    u.phone_number,
    u.role AS user_role_name,
    u.tenant_id AS user_tenant_id,
    u.role_id,
    r.name AS linked_role_name,
    r.tenant_id AS role_tenant_id,
    CASE 
        WHEN u.tenant_id = r.tenant_id THEN 'MATCHED (OK)'
        ELSE 'MISMATCH (ERROR)'
    END AS status
FROM users u
LEFT JOIN roles r ON u.role_id = r.id
ORDER BY u.tenant_id, u.name;
