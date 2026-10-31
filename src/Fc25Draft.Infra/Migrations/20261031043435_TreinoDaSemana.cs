using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class TreinoDaSemana : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TreinosSemanais",
                columns: table => new
                {
                    TreinoId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Semana = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Foco = table.Column<short>(type: "smallint", nullable: false),
                    EscolhidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AplicadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Atributo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    OverallAntes = table.Column<int>(type: "integer", nullable: true),
                    OverallDepois = table.Column<int>(type: "integer", nullable: true),
                    SemEfeito = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    EvolucaoPesId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreinosSemanais", x => x.TreinoId);
                    table.ForeignKey(
                        name: "FK_TreinosSemanais_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TreinosSemanais_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TreinosSemanais_AplicadoEm",
                table: "TreinosSemanais",
                column: "AplicadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_TreinosSemanais_PlayerId",
                table: "TreinosSemanais",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TreinosSemanais_TeamId_Semana_PlayerId",
                table: "TreinosSemanais",
                columns: new[] { "TeamId", "Semana", "PlayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TreinosSemanais");
        }
    }
}
