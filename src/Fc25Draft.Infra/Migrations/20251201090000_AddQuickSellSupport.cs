using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    public partial class AddQuickSellSupport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreviousOverall",
                table: "Players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 1);

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

            migrationBuilder.AddColumn<decimal>(
                name: "Payout",
                table: "TransferHistories",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "PerformedAtUtc",
                table: "TransferHistories",
                newName: "OccurredAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_TransferHistories_PlayerId_PerformedAtUtc",
                table: "TransferHistories",
                newName: "IX_TransferHistories_PlayerId_OccurredAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_TransferHistories_PerformedAtUtc",
                table: "TransferHistories",
                newName: "IX_TransferHistories_OccurredAtUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_TransferHistories_PlayerId_OccurredAtUtc",
                table: "TransferHistories",
                newName: "IX_TransferHistories_PlayerId_PerformedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_TransferHistories_OccurredAtUtc",
                table: "TransferHistories",
                newName: "IX_TransferHistories_PerformedAtUtc");

            migrationBuilder.RenameColumn(
                name: "OccurredAtUtc",
                table: "TransferHistories",
                newName: "PerformedAtUtc");

            migrationBuilder.DropColumn(
                name: "NewOverall",
                table: "TransferHistories");

            migrationBuilder.DropColumn(
                name: "OldOverall",
                table: "TransferHistories");

            migrationBuilder.DropColumn(
                name: "Payout",
                table: "TransferHistories");

            migrationBuilder.DropColumn(
                name: "PreviousOverall",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Players");
        }
    }
}
