using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorCampo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sync_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Adapter = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Entity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ItemsProcessed = table.Column<int>(type: "integer", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sync_logs_Adapter_StartedAt",
                table: "sync_logs",
                columns: new[] { "Adapter", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_logs");
        }
    }
}
