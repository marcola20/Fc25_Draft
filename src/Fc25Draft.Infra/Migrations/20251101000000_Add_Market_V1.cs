using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Add_Market_V1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bids");

            migrationBuilder.DropTable(
                name: "TransferMarketItems");

            migrationBuilder.DropTable(
                name: "TeamBudgets");

            migrationBuilder.DropTable(
                name: "TransferHistories");

            migrationBuilder.DropIndex(
                name: "IX_Teams_TeamToken",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "TeamToken",
                table: "Teams");

            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "Teams",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Budget",
                table: "Teams",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetBlocked",
                table: "Teams",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentTeamId",
                table: "Players",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MarketCycles",
                columns: table => new
                {
                    CycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextCycleAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketCycles", x => x.CycleId);
                });

            migrationBuilder.CreateTable(
                name: "MarketItems",
                columns: table => new
                {
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BuyNowPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinIncrement = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentLeaderTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentLeaderAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WinnerTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketItems", x => x.ItemId);
                    table.ForeignKey(
                        name: "FK_MarketItems_MarketCycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "MarketCycles",
                        principalColumn: "CycleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketItems_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketItems_Teams_CurrentLeaderTeamId",
                        column: x => x.CurrentLeaderTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketItems_Teams_WinnerTeamId",
                        column: x => x.WinnerTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketBids",
                columns: table => new
                {
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketBids", x => x.BidId);
                    table.ForeignKey(
                        name: "FK_MarketBids_MarketItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "MarketItems",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketBids_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransferHistories",
                columns: table => new
                {
                    TransferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    FromTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PerformedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferHistories", x => x.TransferId);
                    table.ForeignKey(
                        name: "FK_TransferHistories_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferHistories_Teams_FromTeamId",
                        column: x => x.FromTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TransferHistories_Teams_ToTeamId",
                        column: x => x.ToTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketBids_ItemId_CreatedAtUtc",
                table: "MarketBids",
                columns: new[] { "ItemId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketBids_TeamId",
                table: "MarketBids",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketItems_CycleId_Status_ExpiresAtUtc",
                table: "MarketItems",
                columns: new[] { "CycleId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketItems_CurrentLeaderTeamId",
                table: "MarketItems",
                column: "CurrentLeaderTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketItems_PlayerId",
                table: "MarketItems",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketItems_WinnerTeamId",
                table: "MarketItems",
                column: "WinnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketCycles_Status",
                table: "MarketCycles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Players_CurrentTeamId",
                table: "Players",
                column: "CurrentTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Token",
                table: "Teams",
                column: "Token",
                unique: true);

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

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Teams_CurrentTeamId",
                table: "Players",
                column: "CurrentTeamId",
                principalTable: "Teams",
                principalColumn: "TeamId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Teams_CurrentTeamId",
                table: "Players");

            migrationBuilder.DropTable(
                name: "MarketBids");

            migrationBuilder.DropTable(
                name: "MarketItems");

            migrationBuilder.DropTable(
                name: "TransferHistories");

            migrationBuilder.DropTable(
                name: "MarketCycles");

            migrationBuilder.DropIndex(
                name: "IX_Players_CurrentTeamId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Teams_Token",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "CurrentTeamId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "Budget",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "BudgetBlocked",
                table: "Teams");

            migrationBuilder.AddColumn<Guid>(
                name: "TeamToken",
                table: "Teams",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

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
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VencedorTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferMarketItems", x => x.MarketItemId);
                    table.CheckConstraint("CK_TransferMarketItem_Status", "[Status] IN ('OPEN','SOLD','EXPIRED')");
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
                });

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
                        name: "FK_Bids_TransferMarketItems_MarketItemId",
                        column: x => x.MarketItemId,
                        principalTable: "TransferMarketItems",
                        principalColumn: "MarketItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bids_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_TeamToken",
                table: "Teams",
                column: "TeamToken");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_MarketItemId",
                table: "Bids",
                column: "MarketItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_TeamId",
                table: "Bids",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferMarketItem_Player_Open",
                table: "TransferMarketItems",
                column: "PlayerId",
                unique: true,
                filter: "[Status] = 'OPEN'");

            migrationBuilder.CreateIndex(
                name: "IX_TransferMarketItem_Player_Status",
                table: "TransferMarketItems",
                columns: new[] { "PlayerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferMarketItem_Status",
                table: "TransferMarketItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TransferMarketItems_MaiorLanceTeamId",
                table: "TransferMarketItems",
                column: "MaiorLanceTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferMarketItems_VencedorTeamId",
                table: "TransferMarketItems",
                column: "VencedorTeamId");

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
        }
    }
}
