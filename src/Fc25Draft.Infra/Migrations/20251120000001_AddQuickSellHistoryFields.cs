using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    public partial class AddQuickSellHistoryFields : Migration
    {
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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewOverall",
                table: "TransferHistories");

            migrationBuilder.DropColumn(
                name: "OldOverall",
                table: "TransferHistories");
        }
    }
}
