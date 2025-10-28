using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                    table.PrimaryKey("PK_AdminActionsLog", x => x.ActionId);
                });

            migrationBuilder.CreateTable(
                name: "Drafts",
                columns: table => new
                {
                    DraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalTeams = table.Column<int>(type: "int", nullable: false),
                    TotalRounds = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drafts", x => x.DraftId);
                });

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
                name: "Positions",
                columns: table => new
                {
                    PositionId = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.PositionId);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Token = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Budget = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    BudgetBlocked = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.TeamId);
                });

            migrationBuilder.CreateTable(
                name: "Token_Administrador",
                columns: table => new
                {
                    AdminTokenId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Token = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Token_Administrador", x => x.AdminTokenId);
                });

            migrationBuilder.CreateTable(
                name: "DraftRounds",
                columns: table => new
                {
                    DraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoundNumber = table.Column<int>(type: "int", nullable: false),
                    OverallMin = table.Column<int>(type: "int", nullable: true),
                    OverallMax = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftRounds", x => new { x.DraftId, x.RoundNumber });
                    table.ForeignKey(
                        name: "FK_DraftRounds_Drafts_DraftId",
                        column: x => x.DraftId,
                        principalTable: "Drafts",
                        principalColumn: "DraftId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BudgetLedgers",
                columns: table => new
                {
                    BudgetLedgerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Origem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetLedgers", x => x.BudgetLedgerId);
                    table.CheckConstraint("CK_BudgetLedger_Tipo", "[Tipo] IN ('CREDIT','DEBIT')");
                    table.CheckConstraint("CK_BudgetLedger_Valor", "[Valor] > 0");
                    table.ForeignKey(
                        name: "FK_BudgetLedgers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Age = table.Column<int>(type: "int", nullable: true),
                    Overall = table.Column<int>(type: "int", nullable: false),
                    PositionId = table.Column<short>(type: "smallint", nullable: false),
                    CurrentTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.PlayerId);
                    table.ForeignKey(
                        name: "FK_Players_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "PositionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Players_Teams_CurrentTeamId",
                        column: x => x.CurrentTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DraftPicks",
                columns: table => new
                {
                    DraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OverallPick = table.Column<int>(type: "int", nullable: false),
                    RoundNumber = table.Column<int>(type: "int", nullable: false),
                    PickInRound = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: true),
                    PickedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftPicks", x => new { x.DraftId, x.OverallPick });
                    table.ForeignKey(
                        name: "FK_DraftPicks_DraftRounds_DraftId_RoundNumber",
                        columns: x => new { x.DraftId, x.RoundNumber },
                        principalTable: "DraftRounds",
                        principalColumns: new[] { "DraftId", "RoundNumber" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DraftPicks_Drafts_DraftId",
                        column: x => x.DraftId,
                        principalTable: "Drafts",
                        principalColumn: "DraftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DraftPicks_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DraftPicks_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
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
                name: "TeamRosters",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamRosters", x => new { x.TeamId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_TeamRosters_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamRosters_Teams_TeamId",
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
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_TransferHistories_Teams_ToTeamId",
                        column: x => x.ToTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId");
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
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Positions",
                columns: new[] { "PositionId", "Name" },
                values: new object[,]
                {
                    { (short)1, "Goleiro" },
                    { (short)2, "Zagueiro" },
                    { (short)3, "Lateral/Ala Esquerdo" },
                    { (short)4, "Lateral/Ala Direito" },
                    { (short)5, "Volante" },
                    { (short)6, "Meia Central" },
                    { (short)7, "Meia Atacante" },
                    { (short)8, "Meia/Ponta Esquerda" },
                    { (short)9, "Meia/Ponta Direita" },
                    { (short)10, "Atacante" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminActionsLog_ActionType_CreatedAtUtc",
                table: "AdminActionsLog",
                columns: new[] { "ActionType", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLedger_TeamId_DataUtc",
                table: "BudgetLedgers",
                columns: new[] { "TeamId", "DataUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_DraftPicks_DraftId_RoundNumber_PickInRound",
                table: "DraftPicks",
                columns: new[] { "DraftId", "RoundNumber", "PickInRound" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DraftPicks_DraftId_TeamId_RoundNumber",
                table: "DraftPicks",
                columns: new[] { "DraftId", "TeamId", "RoundNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_DraftPicks_PlayerId",
                table: "DraftPicks",
                column: "PlayerId",
                unique: true,
                filter: "[PlayerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DraftPicks_TeamId",
                table: "DraftPicks",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketBids_ItemId_CreatedAtUtc",
                table: "MarketBids",
                columns: new[] { "ItemId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketBids_TeamId",
                table: "MarketBids",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketItems_CurrentLeaderTeamId",
                table: "MarketItems",
                column: "CurrentLeaderTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketItems_CycleId_Status_ExpiresAtUtc",
                table: "MarketItems",
                columns: new[] { "CycleId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketItems_Player",
                table: "MarketItems",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketItems_WinnerTeamId",
                table: "MarketItems",
                column: "WinnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_CurrentTeamId",
                table: "Players",
                column: "CurrentTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Name_PositionId",
                table: "Players",
                columns: new[] { "Name", "PositionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Players_PlayerGuid",
                table: "Players",
                column: "PlayerGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_PositionId",
                table: "Players",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_Name",
                table: "Positions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamRosters_PlayerId",
                table: "TeamRosters",
                column: "PlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_TeamName",
                table: "Teams",
                column: "TeamName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Token",
                table: "Teams",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Token_Administrador_Token",
                table: "Token_Administrador",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminActionsLog");

            migrationBuilder.DropTable(
                name: "BudgetLedgers");

            migrationBuilder.DropTable(
                name: "DraftPicks");

            migrationBuilder.DropTable(
                name: "MarketBids");

            migrationBuilder.DropTable(
                name: "TeamRosters");

            migrationBuilder.DropTable(
                name: "Token_Administrador");

            migrationBuilder.DropTable(
                name: "TransferHistories");

            migrationBuilder.DropTable(
                name: "DraftRounds");

            migrationBuilder.DropTable(
                name: "MarketItems");

            migrationBuilder.DropTable(
                name: "Drafts");

            migrationBuilder.DropTable(
                name: "MarketCycles");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "Teams");
        }
    }
}
