using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AdminTransfersPhaseB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminActionsLog",
                columns: table => new
                {
                    ActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    PerformedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminActionsLogs", x => x.ActionId);
                });

            migrationBuilder.DropIndex(
                name: "IX_TransferHistories_FromTeamId",
                table: "TransferHistories");

            migrationBuilder.DropIndex(
                name: "IX_TransferHistories_PlayerId_PerformedAtUtc",
                table: "TransferHistories");

            migrationBuilder.DropIndex(
                name: "IX_TransferHistories_ToTeamId",
                table: "TransferHistories");

            migrationBuilder.CreateIndex(
                name: "IX_AdminActionsLogs_ActionType_CreatedAtUtc",
                table: "AdminActionsLog",
                columns: new[] { "ActionType", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferHistories_FromTeam",
                table: "TransferHistories",
                columns: new[] { "FromTeamId", "PerformedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferHistories_PerformedAtUtc",
                table: "TransferHistories",
                column: "PerformedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TransferHistories_Player",
                table: "TransferHistories",
                columns: new[] { "PlayerId", "PerformedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferHistories_ToTeam",
                table: "TransferHistories",
                columns: new[] { "ToTeamId", "PerformedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminActionsLog");

            migrationBuilder.DropIndex(
                name: "IX_TransferHistories_FromTeam",
                table: "TransferHistories");

            migrationBuilder.DropIndex(
                name: "IX_TransferHistories_PerformedAtUtc",
                table: "TransferHistories");

            migrationBuilder.DropIndex(
                name: "IX_TransferHistories_Player",
                table: "TransferHistories");

            migrationBuilder.DropIndex(
                name: "IX_TransferHistories_ToTeam",
                table: "TransferHistories");

            migrationBuilder.CreateIndex(
                name: "IX_TransferHistories_FromTeamId",
                table: "TransferHistories",
                column: "FromTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferHistories_PlayerId_PerformedAtUtc",
                table: "TransferHistories",
                columns: new[] { "PlayerId", "PerformedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferHistories_ToTeamId",
                table: "TransferHistories",
                column: "ToTeamId");
        }
    }
}
