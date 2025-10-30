using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketTransactions",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PerformedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketTransactions", x => x.TransactionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTransactions_Cycle",
                table: "MarketTransactions",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTransactions_Item",
                table: "MarketTransactions",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTransactions_Player",
                table: "MarketTransactions",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTransactions_Item_Type_CreatedAt",
                table: "MarketTransactions",
                columns: new[] { "ItemId", "Type", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTransactions_Cycle_Type_CreatedAt",
                table: "MarketTransactions",
                columns: new[] { "CycleId", "Type", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTransactions_Player_CreatedAt",
                table: "MarketTransactions",
                columns: new[] { "PlayerId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketTransactions");
        }
    }
}
