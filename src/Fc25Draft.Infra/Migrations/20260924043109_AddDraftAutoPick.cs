using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftAutoPick : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Automatica",
                table: "DraftPicks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DraftAutoPicks",
                columns: table => new
                {
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Modo = table.Column<int>(type: "integer", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftAutoPicks", x => new { x.DraftId, x.TeamId });
                    table.ForeignKey(
                        name: "FK_DraftAutoPicks_Drafts_DraftId",
                        column: x => x.DraftId,
                        principalTable: "Drafts",
                        principalColumn: "DraftId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DraftAutoPicks_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DraftAutoPickItens",
                columns: table => new
                {
                    DraftAutoPickItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<short>(type: "smallint", nullable: true),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftAutoPickItens", x => x.DraftAutoPickItemId);
                    table.ForeignKey(
                        name: "FK_DraftAutoPickItens_DraftAutoPicks_DraftId_TeamId",
                        columns: x => new { x.DraftId, x.TeamId },
                        principalTable: "DraftAutoPicks",
                        principalColumns: new[] { "DraftId", "TeamId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DraftAutoPickItens_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DraftAutoPickRodadas",
                columns: table => new
                {
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    PositionId = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftAutoPickRodadas", x => new { x.DraftId, x.TeamId, x.RoundNumber });
                    table.ForeignKey(
                        name: "FK_DraftAutoPickRodadas_DraftAutoPicks_DraftId_TeamId",
                        columns: x => new { x.DraftId, x.TeamId },
                        principalTable: "DraftAutoPicks",
                        principalColumns: new[] { "DraftId", "TeamId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DraftAutoPickRodadas_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "PositionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DraftAutoPickItens_DraftId_TeamId_PlayerId",
                table: "DraftAutoPickItens",
                columns: new[] { "DraftId", "TeamId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DraftAutoPickItens_DraftId_TeamId_PositionId_Ordem",
                table: "DraftAutoPickItens",
                columns: new[] { "DraftId", "TeamId", "PositionId", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_DraftAutoPickItens_PlayerId",
                table: "DraftAutoPickItens",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftAutoPickRodadas_PositionId",
                table: "DraftAutoPickRodadas",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftAutoPicks_TeamId",
                table: "DraftAutoPicks",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DraftAutoPickItens");

            migrationBuilder.DropTable(
                name: "DraftAutoPickRodadas");

            migrationBuilder.DropTable(
                name: "DraftAutoPicks");

            migrationBuilder.DropColumn(
                name: "Automatica",
                table: "DraftPicks");
        }
    }
}
