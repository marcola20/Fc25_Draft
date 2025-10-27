using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Market_Negotiations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    table.CheckConstraint(
                        name: "CK_Negotiation_Status",
                        sql: "[Status] IN ('PENDING','ACCEPTED','REJECTED','CANCELLED','COMPLETED')");
                    table.CheckConstraint(
                        name: "CK_Negotiation_Tipo",
                        sql: "[Tipo] IN ('TRADE','SALE')");
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
                    table.CheckConstraint(
                        name: "CK_NegotiationPlayer_Papel",
                        sql: "[Papel] IN ('OFFERED','REQUESTED')");
                });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NegotiationPlayers");

            migrationBuilder.DropTable(
                name: "Negotiations");
        }
    }
}
