using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransferConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MaxQuickSellPerWindow = table.Column<int>(type: "integer", nullable: false),
                    MaxTransfers = table.Column<int>(type: "integer", nullable: false),
                    MinRosterSize = table.Column<int>(type: "integer", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferConfigs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransferConfigs");
        }
    }
}
