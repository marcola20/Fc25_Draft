using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AssociacaoTreinadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssociacaoEleicoes",
                columns: table => new
                {
                    EleicaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Turno = table.Column<int>(type: "integer", nullable: false),
                    Situacao = table.Column<int>(type: "integer", nullable: false),
                    InscricoesAte = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AbreEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FechaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Edital = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    Comissao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    ChapaVencedoraId = table.Column<Guid>(type: "uuid", nullable: true),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    VotacaoAbertaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EncerradaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResultadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssociacaoEleicoes", x => x.EleicaoId);
                });

            migrationBuilder.CreateTable(
                name: "AssociacaoChapas",
                columns: table => new
                {
                    ChapaId = table.Column<Guid>(type: "uuid", nullable: false),
                    EleicaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Plano = table.Column<string>(type: "text", nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssociacaoChapas", x => x.ChapaId);
                    table.ForeignKey(
                        name: "FK_AssociacaoChapas_AssociacaoEleicoes_EleicaoId",
                        column: x => x.EleicaoId,
                        principalTable: "AssociacaoEleicoes",
                        principalColumn: "EleicaoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssociacaoEleitores",
                columns: table => new
                {
                    EleicaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreinadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VotouEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssociacaoEleitores", x => new { x.EleicaoId, x.TreinadorId });
                    table.ForeignKey(
                        name: "FK_AssociacaoEleitores_AssociacaoEleicoes_EleicaoId",
                        column: x => x.EleicaoId,
                        principalTable: "AssociacaoEleicoes",
                        principalColumn: "EleicaoId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssociacaoEleitores_Treinadores_TreinadorId",
                        column: x => x.TreinadorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssociacaoChapaMembros",
                columns: table => new
                {
                    ChapaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cargo = table.Column<int>(type: "integer", nullable: false),
                    TreinadorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssociacaoChapaMembros", x => new { x.ChapaId, x.Cargo });
                    table.ForeignKey(
                        name: "FK_AssociacaoChapaMembros_AssociacaoChapas_ChapaId",
                        column: x => x.ChapaId,
                        principalTable: "AssociacaoChapas",
                        principalColumn: "ChapaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssociacaoChapaMembros_Treinadores_TreinadorId",
                        column: x => x.TreinadorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssociacaoVotos",
                columns: table => new
                {
                    VotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    EleicaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapaId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssociacaoVotos", x => x.VotoId);
                    table.ForeignKey(
                        name: "FK_AssociacaoVotos_AssociacaoChapas_ChapaId",
                        column: x => x.ChapaId,
                        principalTable: "AssociacaoChapas",
                        principalColumn: "ChapaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssociacaoVotos_AssociacaoEleicoes_EleicaoId",
                        column: x => x.EleicaoId,
                        principalTable: "AssociacaoEleicoes",
                        principalColumn: "EleicaoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssociacaoChapaMembros_TreinadorId",
                table: "AssociacaoChapaMembros",
                column: "TreinadorId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociacaoChapas_EleicaoId_Numero",
                table: "AssociacaoChapas",
                columns: new[] { "EleicaoId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssociacaoEleicoes_CriadaEm",
                table: "AssociacaoEleicoes",
                column: "CriadaEm");

            migrationBuilder.CreateIndex(
                name: "IX_AssociacaoEleitores_TreinadorId",
                table: "AssociacaoEleitores",
                column: "TreinadorId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociacaoVotos_ChapaId",
                table: "AssociacaoVotos",
                column: "ChapaId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociacaoVotos_EleicaoId",
                table: "AssociacaoVotos",
                column: "EleicaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssociacaoChapaMembros");

            migrationBuilder.DropTable(
                name: "AssociacaoEleitores");

            migrationBuilder.DropTable(
                name: "AssociacaoVotos");

            migrationBuilder.DropTable(
                name: "AssociacaoChapas");

            migrationBuilder.DropTable(
                name: "AssociacaoEleicoes");
        }
    }
}
