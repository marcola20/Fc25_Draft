using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddPremiacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Premiacoes",
                columns: table => new
                {
                    PremiacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Premiacoes", x => x.PremiacaoId);
                });

            migrationBuilder.CreateTable(
                name: "PremiacaoItens",
                columns: table => new
                {
                    PremiacaoItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    PremiacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Divisao = table.Column<int>(type: "integer", nullable: true),
                    PosicaoDe = table.Column<int>(type: "integer", nullable: true),
                    PosicaoAte = table.Column<int>(type: "integer", nullable: true),
                    Fase = table.Column<int>(type: "integer", nullable: true),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiacaoItens", x => x.PremiacaoItemId);
                    table.ForeignKey(
                        name: "FK_PremiacaoItens_Premiacoes_PremiacaoId",
                        column: x => x.PremiacaoId,
                        principalTable: "Premiacoes",
                        principalColumn: "PremiacaoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PremiacaoPagamentos",
                columns: table => new
                {
                    PagamentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PremiacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    LigaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PagoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiacaoPagamentos", x => x.PagamentoId);
                    table.ForeignKey(
                        name: "FK_PremiacaoPagamentos_Ligas_LigaId",
                        column: x => x.LigaId,
                        principalTable: "Ligas",
                        principalColumn: "LigaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PremiacaoPagamentos_Premiacoes_PremiacaoId",
                        column: x => x.PremiacaoId,
                        principalTable: "Premiacoes",
                        principalColumn: "PremiacaoId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PremiacaoPagamentos_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PremiacaoItens_PremiacaoId_Tipo_Divisao",
                table: "PremiacaoItens",
                columns: new[] { "PremiacaoId", "Tipo", "Divisao" });

            migrationBuilder.CreateIndex(
                name: "IX_PremiacaoPagamentos_LigaId_TimeId",
                table: "PremiacaoPagamentos",
                columns: new[] { "LigaId", "TimeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PremiacaoPagamentos_PremiacaoId",
                table: "PremiacaoPagamentos",
                column: "PremiacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_PremiacaoPagamentos_TimeId",
                table: "PremiacaoPagamentos",
                column: "TimeId");

            migrationBuilder.CreateIndex(
                name: "IX_Premiacoes_Temporada",
                table: "Premiacoes",
                column: "Temporada",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PremiacaoItens");

            migrationBuilder.DropTable(
                name: "PremiacaoPagamentos");

            migrationBuilder.DropTable(
                name: "Premiacoes");
        }
    }
}
