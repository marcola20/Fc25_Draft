using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddLigaSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ligas",
                columns: table => new
                {
                    LigaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TotalRodadas = table.Column<int>(type: "integer", nullable: false, defaultValue: 8),
                    DataInicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DataFim = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ligas", x => x.LigaId);
                });

            migrationBuilder.CreateTable(
                name: "LigaClassificacoes",
                columns: table => new
                {
                    ClassificacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    LigaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Posicao = table.Column<int>(type: "integer", nullable: false),
                    Pontos = table.Column<int>(type: "integer", nullable: false),
                    Jogos = table.Column<int>(type: "integer", nullable: false),
                    Vitorias = table.Column<int>(type: "integer", nullable: false),
                    Empates = table.Column<int>(type: "integer", nullable: false),
                    Derrotas = table.Column<int>(type: "integer", nullable: false),
                    GolsPro = table.Column<int>(type: "integer", nullable: false),
                    GolsContra = table.Column<int>(type: "integer", nullable: false),
                    SaldoGols = table.Column<int>(type: "integer", nullable: false),
                    CartoesAmarelos = table.Column<int>(type: "integer", nullable: false),
                    CartoesVermelhos = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigaClassificacoes", x => x.ClassificacaoId);
                    table.ForeignKey(
                        name: "FK_LigaClassificacoes_Ligas_LigaId",
                        column: x => x.LigaId,
                        principalTable: "Ligas",
                        principalColumn: "LigaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LigaClassificacoes_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LigaPunicoes",
                columns: table => new
                {
                    PunicaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    LigaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PontosSubtraidos = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigaPunicoes", x => x.PunicaoId);
                    table.ForeignKey(
                        name: "FK_LigaPunicoes_Ligas_LigaId",
                        column: x => x.LigaId,
                        principalTable: "Ligas",
                        principalColumn: "LigaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LigaPunicoes_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LigaRodadas",
                columns: table => new
                {
                    RodadaId = table.Column<Guid>(type: "uuid", nullable: false),
                    LigaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigaRodadas", x => x.RodadaId);
                    table.ForeignKey(
                        name: "FK_LigaRodadas_Ligas_LigaId",
                        column: x => x.LigaId,
                        principalTable: "Ligas",
                        principalColumn: "LigaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LigaPartidas",
                columns: table => new
                {
                    PartidaId = table.Column<Guid>(type: "uuid", nullable: false),
                    RodadaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeCasaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeForaId = table.Column<Guid>(type: "uuid", nullable: false),
                    GolsCasa = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    GolsFora = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsWO = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TemPenaltis = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PenaltisVencedorId = table.Column<Guid>(type: "uuid", nullable: true),
                    IniciadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EncerradaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigaPartidas", x => x.PartidaId);
                    table.ForeignKey(
                        name: "FK_LigaPartidas_LigaRodadas_RodadaId",
                        column: x => x.RodadaId,
                        principalTable: "LigaRodadas",
                        principalColumn: "RodadaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LigaPartidas_Teams_PenaltisVencedorId",
                        column: x => x.PenaltisVencedorId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LigaPartidas_Teams_TimeCasaId",
                        column: x => x.TimeCasaId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LigaPartidas_Teams_TimeForaId",
                        column: x => x.TimeForaId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LigaEventos",
                columns: table => new
                {
                    EventoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartidaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    AssistenteId = table.Column<int>(type: "integer", nullable: true),
                    Minuto = table.Column<int>(type: "integer", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigaEventos", x => x.EventoId);
                    table.ForeignKey(
                        name: "FK_LigaEventos_LigaPartidas_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "LigaPartidas",
                        principalColumn: "PartidaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LigaEventos_Players_AssistenteId",
                        column: x => x.AssistenteId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LigaEventos_Players_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LigaEventos_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LigaKnockoutJogos",
                columns: table => new
                {
                    KnockoutJogoId = table.Column<Guid>(type: "uuid", nullable: false),
                    LigaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fase = table.Column<int>(type: "integer", nullable: false),
                    TimeCasaId = table.Column<Guid>(type: "uuid", nullable: true),
                    TimeForaId = table.Column<Guid>(type: "uuid", nullable: true),
                    VencedorId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartidaId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigaKnockoutJogos", x => x.KnockoutJogoId);
                    table.ForeignKey(
                        name: "FK_LigaKnockoutJogos_LigaPartidas_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "LigaPartidas",
                        principalColumn: "PartidaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LigaKnockoutJogos_Ligas_LigaId",
                        column: x => x.LigaId,
                        principalTable: "Ligas",
                        principalColumn: "LigaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LigaKnockoutJogos_Teams_TimeCasaId",
                        column: x => x.TimeCasaId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LigaKnockoutJogos_Teams_TimeForaId",
                        column: x => x.TimeForaId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LigaKnockoutJogos_Teams_VencedorId",
                        column: x => x.VencedorId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LigaClassificacoes_LigaId_TimeId",
                table: "LigaClassificacoes",
                columns: new[] { "LigaId", "TimeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LigaClassificacoes_TimeId",
                table: "LigaClassificacoes",
                column: "TimeId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaEventos_AssistenteId",
                table: "LigaEventos",
                column: "AssistenteId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaEventos_JogadorId",
                table: "LigaEventos",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaEventos_PartidaId",
                table: "LigaEventos",
                column: "PartidaId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaEventos_TimeId",
                table: "LigaEventos",
                column: "TimeId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaKnockoutJogos_LigaId_Fase",
                table: "LigaKnockoutJogos",
                columns: new[] { "LigaId", "Fase" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LigaKnockoutJogos_PartidaId",
                table: "LigaKnockoutJogos",
                column: "PartidaId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaKnockoutJogos_TimeCasaId",
                table: "LigaKnockoutJogos",
                column: "TimeCasaId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaKnockoutJogos_TimeForaId",
                table: "LigaKnockoutJogos",
                column: "TimeForaId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaKnockoutJogos_VencedorId",
                table: "LigaKnockoutJogos",
                column: "VencedorId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaPartidas_PenaltisVencedorId",
                table: "LigaPartidas",
                column: "PenaltisVencedorId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaPartidas_RodadaId",
                table: "LigaPartidas",
                column: "RodadaId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaPartidas_TimeCasaId",
                table: "LigaPartidas",
                column: "TimeCasaId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaPartidas_TimeForaId",
                table: "LigaPartidas",
                column: "TimeForaId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaPunicoes_LigaId",
                table: "LigaPunicoes",
                column: "LigaId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaPunicoes_TimeId",
                table: "LigaPunicoes",
                column: "TimeId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaRodadas_LigaId_Numero",
                table: "LigaRodadas",
                columns: new[] { "LigaId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LigaClassificacoes");

            migrationBuilder.DropTable(
                name: "LigaEventos");

            migrationBuilder.DropTable(
                name: "LigaKnockoutJogos");

            migrationBuilder.DropTable(
                name: "LigaPunicoes");

            migrationBuilder.DropTable(
                name: "LigaPartidas");

            migrationBuilder.DropTable(
                name: "LigaRodadas");

            migrationBuilder.DropTable(
                name: "Ligas");
        }
    }
}
