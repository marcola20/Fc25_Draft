using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class EstatisticasDosJogadoresPorPartida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LigaEstatisticasJogadores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartidaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: true),
                    NomePes = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Posicao = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    Grupo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    NotaPes = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: true),
                    MelhorEmCampo = table.Column<bool>(type: "boolean", nullable: false),
                    Minutos = table.Column<int>(type: "integer", nullable: true),
                    Gols = table.Column<int>(type: "integer", nullable: true),
                    GolsDePenalti = table.Column<int>(type: "integer", nullable: true),
                    GolsDeFalta = table.Column<int>(type: "integer", nullable: true),
                    Assistencias = table.Column<int>(type: "integer", nullable: true),
                    Chutes = table.Column<int>(type: "integer", nullable: true),
                    ChutesAGol = table.Column<int>(type: "integer", nullable: true),
                    Passes = table.Column<int>(type: "integer", nullable: true),
                    PassesCertos = table.Column<int>(type: "integer", nullable: true),
                    Cruzamentos = table.Column<int>(type: "integer", nullable: true),
                    Faltas = table.Column<int>(type: "integer", nullable: true),
                    Impedimentos = table.Column<int>(type: "integer", nullable: true),
                    CobrancasDeFalta = table.Column<int>(type: "integer", nullable: true),
                    Escanteios = table.Column<int>(type: "integer", nullable: true),
                    Interceptacoes = table.Column<int>(type: "integer", nullable: true),
                    Toques = table.Column<int>(type: "integer", nullable: true),
                    DistanciaDeDrible = table.Column<decimal>(type: "numeric(7,1)", precision: 7, scale: 1, nullable: true),
                    VelocidadeMedia = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    Desarmes = table.Column<int>(type: "integer", nullable: true),
                    DesarmesCertos = table.Column<int>(type: "integer", nullable: true),
                    Afastamentos = table.Column<int>(type: "integer", nullable: true),
                    ChutesSofridos = table.Column<int>(type: "integer", nullable: true),
                    ChutesSofridosAGol = table.Column<int>(type: "integer", nullable: true),
                    Defesas = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigaEstatisticasJogadores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LigaEstatisticasJogadores_LigaPartidas_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "LigaPartidas",
                        principalColumn: "PartidaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LigaEstatisticasJogadores_Players_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LigaEstatisticasJogadores_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LigaEstatisticasJogadores_JogadorId",
                table: "LigaEstatisticasJogadores",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaEstatisticasJogadores_PartidaId_JogadorId",
                table: "LigaEstatisticasJogadores",
                columns: new[] { "PartidaId", "JogadorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LigaEstatisticasJogadores_TimeId",
                table: "LigaEstatisticasJogadores",
                column: "TimeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LigaEstatisticasJogadores");
        }
    }
}
