using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackLord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSettingsAndLotClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "settings_json",
                table: "tenants",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<Guid>(
                name: "client_id",
                table: "production_lots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "client_name",
                table: "production_lots",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_lots_client_id",
                table: "production_lots",
                column: "client_id");

            migrationBuilder.AddForeignKey(
                name: "FK_production_lots_clients_client_id",
                table: "production_lots",
                column: "client_id",
                principalTable: "clients",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_production_lots_clients_client_id",
                table: "production_lots");

            migrationBuilder.DropIndex(
                name: "IX_production_lots_client_id",
                table: "production_lots");

            migrationBuilder.DropColumn(
                name: "settings_json",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "client_id",
                table: "production_lots");

            migrationBuilder.DropColumn(
                name: "client_name",
                table: "production_lots");
        }
    }
}
