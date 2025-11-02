using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferHistoryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TransferHistories_PerformedAtUtc",
                table: "TransferHistories",
                column: "PerformedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TransferHistories_FromTeamId",
                table: "TransferHistories",
                column: "FromTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferHistories_ToTeamId",
                table: "TransferHistories",
                column: "ToTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferHistories_PerformedAtUtc",
                table: "TransferHistories");

            migrationBuilder.DropIndex(
                name: "IX_TransferHistories_FromTeamId",
                table: "TransferHistories");

            migrationBuilder.DropIndex(
                name: "IX_TransferHistories_ToTeamId",
                table: "TransferHistories");
        }
    }
}
