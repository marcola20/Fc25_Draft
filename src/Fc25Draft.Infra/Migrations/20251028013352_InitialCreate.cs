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
                    TeamToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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
                name: "Players",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Age = table.Column<int>(type: "int", nullable: true),
                    Overall = table.Column<int>(type: "int", nullable: false),
                    PositionId = table.Column<short>(type: "smallint", nullable: false)
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
                name: "Negotiations",
                columns: table => new
                {
                    NegotiationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrigemTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinoTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValorOferecido = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DataInicioUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataFechamentoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Negotiations", x => x.NegotiationId);
                    table.CheckConstraint("CK_Negotiation_Status", "[Status] IN ('PENDING','ACCEPTED','REJECTED','CANCELLED','COMPLETED')");
                    table.CheckConstraint("CK_Negotiation_Tipo", "[Tipo] IN ('TRADE','SALE')");
                    table.ForeignKey(
                        name: "FK_Negotiations_Teams_DestinoTeamId",
                        column: x => x.DestinoTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Negotiations_Teams_OrigemTeamId",
                        column: x => x.OrigemTeamId,
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
                    table.CheckConstraint("CK_TransferHistory_Tipo", "[Tipo] IN ('MARKET_AUCTION','TEAM_SALE','TEAM_TRADE')");
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
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
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
                name: "NegotiationPlayers",
                columns: table => new
                {
                    NegotiationPlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NegotiationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Papel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NegotiationPlayers", x => x.NegotiationPlayerId);
                    table.CheckConstraint("CK_NegotiationPlayer_Papel", "[Papel] IN ('OFFERED','REQUESTED')");
                    table.ForeignKey(
                        name: "FK_NegotiationPlayers_Negotiations_NegotiationId",
                        column: x => x.NegotiationId,
                        principalTable: "Negotiations",
                        principalColumn: "NegotiationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NegotiationPlayers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NegotiationPlayers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
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
                name: "IX_Bid_MarketItemId_DataUtc",
                table: "Bids",
                columns: new[] { "MarketItemId", "DataUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Bids_TeamId",
                table: "Bids",
                column: "TeamId");

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
                name: "IX_NegotiationPlayer_NegotiationId",
                table: "NegotiationPlayers",
                column: "NegotiationId");

            migrationBuilder.CreateIndex(
                name: "IX_NegotiationPlayers_PlayerId",
                table: "NegotiationPlayers",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_NegotiationPlayers_TeamId",
                table: "NegotiationPlayers",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Negotiation_Status",
                table: "Negotiations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Negotiations_DestinoTeamId",
                table: "Negotiations",
                column: "DestinoTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Negotiations_OrigemTeamId",
                table: "Negotiations",
                column: "OrigemTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Name_PositionId",
                table: "Players",
                columns: new[] { "Name", "PositionId" });

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
                name: "IX_Teams_TeamToken",
                table: "Teams",
                column: "TeamToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Token_Administrador_Token",
                table: "Token_Administrador",
                column: "Token",
                unique: true);

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
                name: "IX_TransferHistory_DataUtc",
                table: "TransferHistories",
                column: "DataUtc");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bids");

            migrationBuilder.DropTable(
                name: "BudgetLedgers");

            migrationBuilder.DropTable(
                name: "DraftPicks");

            migrationBuilder.DropTable(
                name: "NegotiationPlayers");

            migrationBuilder.DropTable(
                name: "TeamBudgets");

            migrationBuilder.DropTable(
                name: "TeamRosters");

            migrationBuilder.DropTable(
                name: "Token_Administrador");

            migrationBuilder.DropTable(
                name: "TransferHistories");

            migrationBuilder.DropTable(
                name: "TransferMarketItems");

            migrationBuilder.DropTable(
                name: "DraftRounds");

            migrationBuilder.DropTable(
                name: "Negotiations");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Drafts");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Positions");
        }
    }
}
