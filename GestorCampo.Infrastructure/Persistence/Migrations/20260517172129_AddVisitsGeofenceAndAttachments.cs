using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorCampo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitsGeofenceAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CheckInLat",
                table: "visits",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInLng",
                table: "visits",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckOutAt",
                table: "visits",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutLat",
                table: "visits",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutLng",
                table: "visits",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOutOfRange",
                table: "visits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OutOfRangeMeters",
                table: "visits",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "visit_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visit_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_visit_attachments_visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_visit_attachments_VisitId",
                table: "visit_attachments",
                column: "VisitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "visit_attachments");

            migrationBuilder.DropColumn(
                name: "CheckInLat",
                table: "visits");

            migrationBuilder.DropColumn(
                name: "CheckInLng",
                table: "visits");

            migrationBuilder.DropColumn(
                name: "CheckOutAt",
                table: "visits");

            migrationBuilder.DropColumn(
                name: "CheckOutLat",
                table: "visits");

            migrationBuilder.DropColumn(
                name: "CheckOutLng",
                table: "visits");

            migrationBuilder.DropColumn(
                name: "IsOutOfRange",
                table: "visits");

            migrationBuilder.DropColumn(
                name: "OutOfRangeMeters",
                table: "visits");
        }
    }
}
