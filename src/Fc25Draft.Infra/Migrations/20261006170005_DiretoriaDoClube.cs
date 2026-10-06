using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class DiretoriaDoClube : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BonusMetaCopaCumprida",
                table: "Premiacoes",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BonusMetaCopaSuperada",
                table: "Premiacoes",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BonusMetaLigaCumprida",
                table: "Premiacoes",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BonusMetaLigaSuperada",
                table: "Premiacoes",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "DiretoriaPagamentos",
                columns: table => new
                {
                    PagamentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    LigaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PagoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiretoriaPagamentos", x => x.PagamentoId);
                    table.ForeignKey(
                        name: "FK_DiretoriaPagamentos_Ligas_LigaId",
                        column: x => x.LigaId,
                        principalTable: "Ligas",
                        principalColumn: "LigaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiretoriaPagamentos_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetasDiretoria",
                columns: table => new
                {
                    MetaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    LigaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PosicaoEsperada = table.Column<int>(type: "integer", nullable: true),
                    Nota = table.Column<double>(type: "double precision", nullable: true),
                    MediaHistorico = table.Column<double>(type: "double precision", nullable: true),
                    ForcaXI = table.Column<double>(type: "double precision", nullable: true),
                    MetaPosicao = table.Column<int>(type: "integer", nullable: true),
                    Pote = table.Column<int>(type: "integer", nullable: true),
                    MetaFase = table.Column<int>(type: "integer", nullable: true),
                    AjustadaPeloAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetasDiretoria", x => x.MetaId);
                    table.ForeignKey(
                        name: "FK_MetasDiretoria_Ligas_LigaId",
                        column: x => x.LigaId,
                        principalTable: "Ligas",
                        principalColumn: "LigaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MetasDiretoria_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiretoriaPagamentos_LigaId_TimeId",
                table: "DiretoriaPagamentos",
                columns: new[] { "LigaId", "TimeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiretoriaPagamentos_TimeId",
                table: "DiretoriaPagamentos",
                column: "TimeId");

            migrationBuilder.CreateIndex(
                name: "IX_MetasDiretoria_LigaId_TimeId",
                table: "MetasDiretoria",
                columns: new[] { "LigaId", "TimeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetasDiretoria_Temporada",
                table: "MetasDiretoria",
                column: "Temporada");

            migrationBuilder.CreateIndex(
                name: "IX_MetasDiretoria_TimeId",
                table: "MetasDiretoria",
                column: "TimeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiretoriaPagamentos");

            migrationBuilder.DropTable(
                name: "MetasDiretoria");

            migrationBuilder.DropColumn(
                name: "BonusMetaCopaCumprida",
                table: "Premiacoes");

            migrationBuilder.DropColumn(
                name: "BonusMetaCopaSuperada",
                table: "Premiacoes");

            migrationBuilder.DropColumn(
                name: "BonusMetaLigaCumprida",
                table: "Premiacoes");

            migrationBuilder.DropColumn(
                name: "BonusMetaLigaSuperada",
                table: "Premiacoes");
        }
    }
}
