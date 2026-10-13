using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PremiosDaTemporada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VotacoesPremios",
                columns: table => new
                {
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    Situacao = table.Column<int>(type: "integer", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AbertaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EncerradaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PublicadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VotacoesPremios", x => x.Temporada);
                });

            migrationBuilder.CreateTable(
                name: "PremiosCandidatos",
                columns: table => new
                {
                    CandidatoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    Categoria = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: true),
                    TreinadorId = table.Column<Guid>(type: "uuid", nullable: true),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Resumo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Vencedor = table.Column<bool>(type: "boolean", nullable: false),
                    OverParaPlayerId = table.Column<int>(type: "integer", nullable: true),
                    OverAplicadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EvolucaoPesId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiosCandidatos", x => x.CandidatoId);
                    table.ForeignKey(
                        name: "FK_PremiosCandidatos_Players_OverParaPlayerId",
                        column: x => x.OverParaPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PremiosCandidatos_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PremiosCandidatos_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PremiosCandidatos_Treinadores_TreinadorId",
                        column: x => x.TreinadorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PremiosCandidatos_VotacoesPremios_Temporada",
                        column: x => x.Temporada,
                        principalTable: "VotacoesPremios",
                        principalColumn: "Temporada",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PremiosVotos",
                columns: table => new
                {
                    VotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidatoId = table.Column<Guid>(type: "uuid", nullable: false),
                    EleitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VotadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiosVotos", x => x.VotoId);
                    table.ForeignKey(
                        name: "FK_PremiosVotos_PremiosCandidatos_CandidatoId",
                        column: x => x.CandidatoId,
                        principalTable: "PremiosCandidatos",
                        principalColumn: "CandidatoId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PremiosVotos_Treinadores_EleitorId",
                        column: x => x.EleitorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PremiosCandidatos_OverParaPlayerId",
                table: "PremiosCandidatos",
                column: "OverParaPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PremiosCandidatos_PlayerId",
                table: "PremiosCandidatos",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PremiosCandidatos_Temporada_Categoria",
                table: "PremiosCandidatos",
                columns: new[] { "Temporada", "Categoria" });

            migrationBuilder.CreateIndex(
                name: "IX_PremiosCandidatos_TimeId",
                table: "PremiosCandidatos",
                column: "TimeId");

            migrationBuilder.CreateIndex(
                name: "IX_PremiosCandidatos_TreinadorId",
                table: "PremiosCandidatos",
                column: "TreinadorId");

            migrationBuilder.CreateIndex(
                name: "IX_PremiosVotos_CandidatoId_EleitorId",
                table: "PremiosVotos",
                columns: new[] { "CandidatoId", "EleitorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PremiosVotos_EleitorId",
                table: "PremiosVotos",
                column: "EleitorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PremiosVotos");

            migrationBuilder.DropTable(
                name: "PremiosCandidatos");

            migrationBuilder.DropTable(
                name: "VotacoesPremios");
        }
    }
}
