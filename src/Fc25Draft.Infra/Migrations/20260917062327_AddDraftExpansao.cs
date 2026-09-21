using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftExpansao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FatorCompensacao",
                table: "Drafts",
                type: "numeric(6,3)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxPerdasPorTime",
                table: "Drafts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProtecaoEncerradaEm",
                table: "Drafts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProtegidosPorTime",
                table: "Drafts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                table: "Drafts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Compensacao",
                table: "DraftPicks",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FromTeamId",
                table: "DraftPicks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DraftProtecoes",
                columns: table => new
                {
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    EscolhidoPeloTime = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftProtecoes", x => new { x.DraftId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_DraftProtecoes_Drafts_DraftId",
                        column: x => x.DraftId,
                        principalTable: "Drafts",
                        principalColumn: "DraftId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DraftProtecoes_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DraftProtecoes_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DraftPicks_FromTeamId",
                table: "DraftPicks",
                column: "FromTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftProtecoes_DraftId_TeamId",
                table: "DraftProtecoes",
                columns: new[] { "DraftId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_DraftProtecoes_PlayerId",
                table: "DraftProtecoes",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftProtecoes_TeamId",
                table: "DraftProtecoes",
                column: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_DraftPicks_Teams_FromTeamId",
                table: "DraftPicks",
                column: "FromTeamId",
                principalTable: "Teams",
                principalColumn: "TeamId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DraftPicks_Teams_FromTeamId",
                table: "DraftPicks");

            migrationBuilder.DropTable(
                name: "DraftProtecoes");

            migrationBuilder.DropIndex(
                name: "IX_DraftPicks_FromTeamId",
                table: "DraftPicks");

            migrationBuilder.DropColumn(
                name: "FatorCompensacao",
                table: "Drafts");

            migrationBuilder.DropColumn(
                name: "MaxPerdasPorTime",
                table: "Drafts");

            migrationBuilder.DropColumn(
                name: "ProtecaoEncerradaEm",
                table: "Drafts");

            migrationBuilder.DropColumn(
                name: "ProtegidosPorTime",
                table: "Drafts");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Drafts");

            migrationBuilder.DropColumn(
                name: "Compensacao",
                table: "DraftPicks");

            migrationBuilder.DropColumn(
                name: "FromTeamId",
                table: "DraftPicks");
        }
    }
}
