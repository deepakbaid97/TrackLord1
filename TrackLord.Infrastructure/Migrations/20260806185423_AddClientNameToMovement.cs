using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackLord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientNameToMovement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "client_name",
                table: "manufacturing_movements",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "client_name",
                table: "manufacturing_movements");
        }
    }
}
