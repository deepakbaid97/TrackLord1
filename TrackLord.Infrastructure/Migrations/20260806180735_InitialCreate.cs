using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackLord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    production_lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_step_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_step_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    movement_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reference_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_reversed = table.Column<bool>(type: "boolean", nullable: false),
                    reversal_movement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversal_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_movements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sequence_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    factory_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_steps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "material_consumptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    production_lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_consumed = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit = table.Column<int>(type: "integer", nullable: false),
                    consumption_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_consumptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "material_lots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    material_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    initial_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit = table.Column<int>(type: "integer", nullable: false),
                    current_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    current_unit = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_lots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "production_lots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    initial_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit = table.Column<int>(type: "integer", nullable: false),
                    initial_step_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_lots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rejection_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    production_lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_rejected = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    rejection_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rejection_records", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_movements_lot_replay_order",
                table: "manufacturing_movements",
                columns: new[] { "production_lot_id", "movement_date", "created_at_utc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_steps_code",
                table: "manufacturing_steps",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_consumptions_material_lot_id",
                table: "material_consumptions",
                column: "material_lot_id");

            migrationBuilder.CreateIndex(
                name: "IX_material_consumptions_production_lot_id",
                table: "material_consumptions",
                column: "production_lot_id");

            migrationBuilder.CreateIndex(
                name: "IX_material_lots_lot_number",
                table: "material_lots",
                column: "lot_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_lots_lot_number",
                table: "production_lots",
                column: "lot_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rejection_records_production_lot_id",
                table: "rejection_records",
                column: "production_lot_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_movements");

            migrationBuilder.DropTable(
                name: "manufacturing_steps");

            migrationBuilder.DropTable(
                name: "material_consumptions");

            migrationBuilder.DropTable(
                name: "material_lots");

            migrationBuilder.DropTable(
                name: "production_lots");

            migrationBuilder.DropTable(
                name: "rejection_records");
        }
    }
}
