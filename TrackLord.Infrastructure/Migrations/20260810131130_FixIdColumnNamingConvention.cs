using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackLord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixIdColumnNamingConvention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "users",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "tenants",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "rejection_records",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "production_lots",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "material_lots",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "material_consumptions",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "manufacturing_steps",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "manufacturing_movements",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "clients",
                newName: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "id",
                table: "users",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "tenants",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "rejection_records",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "production_lots",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "material_lots",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "material_consumptions",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "manufacturing_steps",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "manufacturing_movements",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "clients",
                newName: "Id");
        }
    }
}
