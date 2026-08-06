using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackLord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientEntityAndMovementForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "client_id",
                table: "manufacturing_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    client_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_movements_client_id",
                table: "manufacturing_movements",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_clients_name",
                table: "clients",
                column: "name");

            migrationBuilder.AddForeignKey(
                name: "FK_manufacturing_movements_clients_client_id",
                table: "manufacturing_movements",
                column: "client_id",
                principalTable: "clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_manufacturing_movements_clients_client_id",
                table: "manufacturing_movements");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropIndex(
                name: "IX_manufacturing_movements_client_id",
                table: "manufacturing_movements");

            migrationBuilder.DropColumn(
                name: "client_id",
                table: "manufacturing_movements");
        }
    }
}
