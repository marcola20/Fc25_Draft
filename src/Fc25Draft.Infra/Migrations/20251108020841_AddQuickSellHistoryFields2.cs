using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddQuickSellHistoryFields2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NewOverall",
                table: "TransferHistories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OldOverall",
                table: "TransferHistories",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferHistories_PerformedAtUtc",
                table: "TransferHistories",
                column: "PerformedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferHistories_PerformedAtUtc",
                table: "TransferHistories");

            migrationBuilder.DropColumn(
                name: "NewOverall",
                table: "TransferHistories");

            migrationBuilder.DropColumn(
                name: "OldOverall",
                table: "TransferHistories");
        }
    }
}
