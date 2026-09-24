using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftAutoPickPrevia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DraftAutoPickPrevias",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Modo = table.Column<int>(type: "integer", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftAutoPickPrevias", x => x.TeamId);
                    table.ForeignKey(
                        name: "FK_DraftAutoPickPrevias_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DraftPlanejadoRodadas",
                columns: table => new
                {
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    OverallMin = table.Column<int>(type: "integer", nullable: true),
                    OverallMax = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftPlanejadoRodadas", x => x.RoundNumber);
                });

            migrationBuilder.CreateTable(
                name: "DraftAutoPickPreviaItens",
                columns: table => new
                {
                    DraftAutoPickPreviaItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<short>(type: "smallint", nullable: true),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftAutoPickPreviaItens", x => x.DraftAutoPickPreviaItemId);
                    table.ForeignKey(
                        name: "FK_DraftAutoPickPreviaItens_DraftAutoPickPrevias_TeamId",
                        column: x => x.TeamId,
                        principalTable: "DraftAutoPickPrevias",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DraftAutoPickPreviaItens_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DraftAutoPickPreviaRodadas",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    PositionId = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftAutoPickPreviaRodadas", x => new { x.TeamId, x.RoundNumber });
                    table.ForeignKey(
                        name: "FK_DraftAutoPickPreviaRodadas_DraftAutoPickPrevias_TeamId",
                        column: x => x.TeamId,
                        principalTable: "DraftAutoPickPrevias",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DraftAutoPickPreviaRodadas_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "PositionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DraftAutoPickPreviaItens_PlayerId",
                table: "DraftAutoPickPreviaItens",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftAutoPickPreviaItens_TeamId_PlayerId",
                table: "DraftAutoPickPreviaItens",
                columns: new[] { "TeamId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DraftAutoPickPreviaRodadas_PositionId",
                table: "DraftAutoPickPreviaRodadas",
                column: "PositionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DraftAutoPickPreviaItens");

            migrationBuilder.DropTable(
                name: "DraftAutoPickPreviaRodadas");

            migrationBuilder.DropTable(
                name: "DraftPlanejadoRodadas");

            migrationBuilder.DropTable(
                name: "DraftAutoPickPrevias");
        }
    }
}
