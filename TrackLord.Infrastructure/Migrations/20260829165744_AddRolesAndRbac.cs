using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackLord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesAndRbac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "role_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_system_role = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    permissions_json = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_role_id",
                table: "users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_tenant_id_name",
                table: "roles",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            // Data Migration: Seed 4 default system roles for each existing tenant and link existing users
            migrationBuilder.Sql(@"
DO $$
DECLARE
    t_rec RECORD;
    v_now TIMESTAMP WITH TIME ZONE := NOW();
    v_admin_role_id      UUID;
    v_supervisor_role_id UUID;
    v_manager_role_id    UUID;
    v_operator_role_id   UUID;
    v_admin_perms        TEXT := '{""production_lots"":[""read"",""write"",""delete""],""movements"":[""read"",""write""],""movement_reversals"":[""write""],""material_lots"":[""read"",""write"",""delete""],""material_consumptions"":[""read"",""write""],""rejection_records"":[""read"",""write""],""manufacturing_steps"":[""read"",""write""],""clients"":[""read"",""write""],""users"":[""read"",""write""],""roles"":[""read"",""write""],""tenant_settings"":[""read"",""write""],""dashboard"":[""read""],""imports"":[""write""]}';
    v_supervisor_perms   TEXT := '{""production_lots"":[""read"",""write""],""movements"":[""read"",""write""],""movement_reversals"":[""write""],""material_lots"":[""read"",""write""],""material_consumptions"":[""read"",""write""],""rejection_records"":[""read"",""write""],""manufacturing_steps"":[""read"",""write""],""clients"":[""read"",""write""],""users"":[""read""],""dashboard"":[""read""],""imports"":[""write""]}';
    v_manager_perms      TEXT := '{""production_lots"":[""read"",""write"",""delete""],""movements"":[""read"",""write""],""movement_reversals"":[""write""],""material_lots"":[""read"",""write""],""material_consumptions"":[""read"",""write""],""rejection_records"":[""read"",""write""],""manufacturing_steps"":[""read"",""write""],""clients"":[""read"",""write""],""users"":[""read"",""write""],""roles"":[""read""],""dashboard"":[""read""],""imports"":[""write""]}';
    v_operator_perms     TEXT := '{""production_lots"":[""read""],""movements"":[""read"",""write""],""manufacturing_steps"":[""read""],""clients"":[""read""],""material_lots"":[""read""],""material_consumptions"":[""read"",""write""],""rejection_records"":[""read"",""write""],""dashboard"":[""read""]}';
BEGIN
    FOR t_rec IN (
        SELECT id AS tenant_id, name, code FROM tenants
        UNION
        SELECT DISTINCT tenant_id, 'Tenant ' || SUBSTRING(tenant_id::text, 1, 8), 'T_' || SUBSTRING(tenant_id::text, 1, 6)
        FROM users 
        WHERE tenant_id IS NOT NULL 
          AND tenant_id NOT IN (SELECT id FROM tenants)
    ) LOOP
        -- Ensure tenant exists in `tenants` table
        INSERT INTO tenants (id, name, code, created_at_utc, is_active)
        VALUES (t_rec.tenant_id, t_rec.name, t_rec.code, v_now, TRUE)
        ON CONFLICT (id) DO NOTHING;

        -- 1. Admin
        SELECT id INTO v_admin_role_id FROM roles WHERE tenant_id = t_rec.tenant_id AND name = 'Admin';
        IF v_admin_role_id IS NULL THEN
            v_admin_role_id := gen_random_uuid();
            INSERT INTO roles (id, tenant_id, name, description, is_system_role, is_active, permissions_json, created_at_utc)
            VALUES (v_admin_role_id, t_rec.tenant_id, 'Admin', 'Unrestricted administrator access across all features', TRUE, TRUE, v_admin_perms, v_now);
        ELSE
            UPDATE roles SET permissions_json = v_admin_perms, is_system_role = TRUE, is_active = TRUE WHERE id = v_admin_role_id;
        END IF;

        -- 2. Supervisor
        SELECT id INTO v_supervisor_role_id FROM roles WHERE tenant_id = t_rec.tenant_id AND name = 'Supervisor';
        IF v_supervisor_role_id IS NULL THEN
            v_supervisor_role_id := gen_random_uuid();
            INSERT INTO roles (id, tenant_id, name, description, is_system_role, is_active, permissions_json, created_at_utc)
            VALUES (v_supervisor_role_id, t_rec.tenant_id, 'Supervisor', 'Production oversight, step configuration, and movement reversals', TRUE, TRUE, v_supervisor_perms, v_now);
        ELSE
            UPDATE roles SET permissions_json = v_supervisor_perms, is_system_role = TRUE, is_active = TRUE WHERE id = v_supervisor_role_id;
        END IF;

        -- 3. Manager
        SELECT id INTO v_manager_role_id FROM roles WHERE tenant_id = t_rec.tenant_id AND name = 'Manager';
        IF v_manager_role_id IS NULL THEN
            v_manager_role_id := gen_random_uuid();
            INSERT INTO roles (id, tenant_id, name, description, is_system_role, is_active, permissions_json, created_at_utc)
            VALUES (v_manager_role_id, t_rec.tenant_id, 'Manager', 'Plant manager with team and operational oversight', TRUE, TRUE, v_manager_perms, v_now);
        ELSE
            UPDATE roles SET permissions_json = v_manager_perms, is_system_role = TRUE, is_active = TRUE WHERE id = v_manager_role_id;
        END IF;

        -- 4. Operator
        SELECT id INTO v_operator_role_id FROM roles WHERE tenant_id = t_rec.tenant_id AND name = 'Operator';
        IF v_operator_role_id IS NULL THEN
            v_operator_role_id := gen_random_uuid();
            INSERT INTO roles (id, tenant_id, name, description, is_system_role, is_active, permissions_json, created_at_utc)
            VALUES (v_operator_role_id, t_rec.tenant_id, 'Operator', 'Factory floor worker for movements, material logs, and defect reporting', TRUE, TRUE, v_operator_perms, v_now);
        ELSE
            UPDATE roles SET permissions_json = v_operator_perms, is_system_role = TRUE, is_active = TRUE WHERE id = v_operator_role_id;
        END IF;

        -- Re-align all users of THIS tenant to point to THIS tenant's own role IDs
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
    END LOOP;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropIndex(
                name: "IX_users_role_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "role_id",
                table: "users");
        }
    }
}
