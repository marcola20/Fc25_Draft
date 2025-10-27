using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class MarketInit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamBudgets",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Saldo = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamBudgets", x => x.TeamId);
                    table.ForeignKey(
                        name: "FK_TeamBudgets_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransferMarketItems",
                columns: table => new
                {
                    MarketItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    PrecoBase = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LanceAtual = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaiorLanceTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrecoComprarAgora = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DataInicioUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataFimUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    VencedorTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferMarketItems", x => x.MarketItemId);
                    table.ForeignKey(
                        name: "FK_TransferMarketItems_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferMarketItems_Teams_MaiorLanceTeamId",
                        column: x => x.MaiorLanceTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferMarketItems_Teams_VencedorTeamId",
                        column: x => x.VencedorTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.CheckConstraint(
                        name: "CK_TransferMarketItem_Status",
                        sql: "[Status] IN ('OPEN','SOLD','EXPIRED')");
                });

            migrationBuilder.CreateTable(
                name: "TransferHistories",
                columns: table => new
                {
                    TransferHistoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    OrigemTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DestinoTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferHistories", x => x.TransferHistoryId);
                    table.ForeignKey(
                        name: "FK_TransferHistories_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferHistories_Teams_DestinoTeamId",
                        column: x => x.DestinoTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferHistories_Teams_OrigemTeamId",
                        column: x => x.OrigemTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.CheckConstraint(
                        name: "CK_TransferHistory_Tipo",
                        sql: "[Tipo] IN ('MARKET_AUCTION','TEAM_SALE','TEAM_TRADE')");
                });

            migrationBuilder.CreateTable(
                name: "Bids",
                columns: table => new
                {
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarketItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DataUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bids", x => x.BidId);
                    table.ForeignKey(
                        name: "FK_Bids_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bids_TransferMarketItems_MarketItemId",
                        column: x => x.MarketItemId,
                        principalTable: "TransferMarketItems",
                        principalColumn: "MarketItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bids_MarketItemId_DataUtc",
                table: "Bids",
                columns: new[] { "MarketItemId", "DataUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Bids_TeamId",
                table: "Bids",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferHistories_DataUtc",
                table: "TransferHistories",
                column: "DataUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TransferHistories_DestinoTeamId",
                table: "TransferHistories",
                column: "DestinoTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferHistories_OrigemTeamId",
                table: "TransferHistories",
                column: "OrigemTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferHistories_PlayerId",
                table: "TransferHistories",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferMarketItems_MaiorLanceTeamId",
                table: "TransferMarketItems",
                column: "MaiorLanceTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferMarketItems_Player_Status",
                table: "TransferMarketItems",
                columns: new[] { "PlayerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferMarketItems_PlayerId",
                table: "TransferMarketItems",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferMarketItems_Status",
                table: "TransferMarketItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TransferMarketItems_VencedorTeamId",
                table: "TransferMarketItems",
                column: "VencedorTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferMarketItems_Player_Open",
                table: "TransferMarketItems",
                column: "PlayerId",
                unique: true,
                filter: "[Status] = 'OPEN'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bids");

            migrationBuilder.DropTable(
                name: "TransferHistories");

            migrationBuilder.DropTable(
                name: "TeamBudgets");

            migrationBuilder.DropTable(
                name: "TransferMarketItems");
        }
    }
}
