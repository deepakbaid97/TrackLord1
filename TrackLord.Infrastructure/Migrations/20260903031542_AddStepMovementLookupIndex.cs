using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackLord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStepMovementLookupIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_movements_to_step_lookup",
                table: "manufacturing_movements",
                columns: new[] { "tenant_id", "to_step_id", "movement_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_movements_to_step_lookup",
                table: "manufacturing_movements");
        }
    }
}
