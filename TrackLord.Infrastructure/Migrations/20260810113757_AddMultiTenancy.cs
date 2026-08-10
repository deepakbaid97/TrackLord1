using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackLord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_phone_number",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_production_lots_lot_number",
                table: "production_lots");

            migrationBuilder.DropIndex(
                name: "IX_material_lots_lot_number",
                table: "material_lots");

            migrationBuilder.DropIndex(
                name: "IX_manufacturing_steps_code",
                table: "manufacturing_steps");

            migrationBuilder.DropIndex(
                name: "ix_movements_lot_replay_order",
                table: "manufacturing_movements");

            migrationBuilder.DropIndex(
                name: "IX_clients_name",
                table: "clients");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "rejection_records",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "production_lots",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "material_lots",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "material_consumptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "manufacturing_steps",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "manufacturing_movements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "clients",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "tenants",
                columns: new[] { "Id", "name", "code", "created_at_utc", "is_active" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), "Default Tenant", "DEFAULT", DateTime.UtcNow, true });

            migrationBuilder.CreateIndex(
                name: "IX_users_tenant_id_phone_number",
                table: "users",
                columns: new[] { "tenant_id", "phone_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_lots_tenant_id_lot_number",
                table: "production_lots",
                columns: new[] { "tenant_id", "lot_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_lots_tenant_id_lot_number",
                table: "material_lots",
                columns: new[] { "tenant_id", "lot_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_steps_tenant_id_code",
                table: "manufacturing_steps",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movements_lot_replay_order",
                table: "manufacturing_movements",
                columns: new[] { "tenant_id", "production_lot_id", "movement_date", "created_at_utc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_clients_tenant_id_name",
                table: "clients",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_code",
                table: "tenants",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_users_tenant_id_phone_number",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_production_lots_tenant_id_lot_number",
                table: "production_lots");

            migrationBuilder.DropIndex(
                name: "IX_material_lots_tenant_id_lot_number",
                table: "material_lots");

            migrationBuilder.DropIndex(
                name: "IX_manufacturing_steps_tenant_id_code",
                table: "manufacturing_steps");

            migrationBuilder.DropIndex(
                name: "ix_movements_lot_replay_order",
                table: "manufacturing_movements");

            migrationBuilder.DropIndex(
                name: "IX_clients_tenant_id_name",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "rejection_records");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "production_lots");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "material_lots");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "material_consumptions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "manufacturing_steps");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "manufacturing_movements");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "clients");

            migrationBuilder.CreateIndex(
                name: "IX_users_phone_number",
                table: "users",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_lots_lot_number",
                table: "production_lots",
                column: "lot_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_lots_lot_number",
                table: "material_lots",
                column: "lot_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_steps_code",
                table: "manufacturing_steps",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movements_lot_replay_order",
                table: "manufacturing_movements",
                columns: new[] { "production_lot_id", "movement_date", "created_at_utc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_clients_name",
                table: "clients",
                column: "name");
        }
    }
}
