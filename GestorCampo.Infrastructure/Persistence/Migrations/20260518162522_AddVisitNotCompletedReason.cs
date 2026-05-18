using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorCampo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitNotCompletedReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NotCompletedReason",
                table: "visits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotCompletedReasonNote",
                table: "visits",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotCompletedReason",
                table: "visits");

            migrationBuilder.DropColumn(
                name: "NotCompletedReasonNote",
                table: "visits");
        }
    }
}
