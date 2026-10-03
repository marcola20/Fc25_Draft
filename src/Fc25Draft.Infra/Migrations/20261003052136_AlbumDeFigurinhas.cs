using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AlbumDeFigurinhas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Albuns",
                columns: table => new
                {
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    LancadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Albuns", x => x.AlbumId);
                });

            migrationBuilder.CreateTable(
                name: "Figurinhas",
                columns: table => new
                {
                    FigurinhaId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Raridade = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: true),
                    NomeImpresso = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PosicaoSigla = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    Overall = table.Column<int>(type: "integer", nullable: true),
                    Destaque = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Figurinhas", x => x.FigurinhaId);
                    table.ForeignKey(
                        name: "FK_Figurinhas_Albuns_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albuns",
                        principalColumn: "AlbumId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Figurinhas_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Figurinhas_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PacotesGanhos",
                columns: table => new
                {
                    PacoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreinadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: true),
                    Origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Chave = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AbertoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Figurinhas = table.Column<Guid[]>(type: "uuid[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PacotesGanhos", x => x.PacoteId);
                    table.ForeignKey(
                        name: "FK_PacotesGanhos_Albuns_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albuns",
                        principalColumn: "AlbumId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PacotesGanhos_Treinadores_TreinadorId",
                        column: x => x.TreinadorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FigurinhasDosTreinadores",
                columns: table => new
                {
                    TreinadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FigurinhaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    PrimeiraEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Nova = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FigurinhasDosTreinadores", x => new { x.TreinadorId, x.FigurinhaId });
                    table.ForeignKey(
                        name: "FK_FigurinhasDosTreinadores_Figurinhas_FigurinhaId",
                        column: x => x.FigurinhaId,
                        principalTable: "Figurinhas",
                        principalColumn: "FigurinhaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FigurinhasDosTreinadores_Treinadores_TreinadorId",
                        column: x => x.TreinadorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Albuns_Temporada",
                table: "Albuns",
                column: "Temporada",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Figurinhas_AlbumId_Numero",
                table: "Figurinhas",
                columns: new[] { "AlbumId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Figurinhas_AlbumId_Raridade",
                table: "Figurinhas",
                columns: new[] { "AlbumId", "Raridade" });

            migrationBuilder.CreateIndex(
                name: "IX_Figurinhas_PlayerId",
                table: "Figurinhas",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Figurinhas_TeamId",
                table: "Figurinhas",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_FigurinhasDosTreinadores_FigurinhaId",
                table: "FigurinhasDosTreinadores",
                column: "FigurinhaId");

            migrationBuilder.CreateIndex(
                name: "IX_PacotesGanhos_AlbumId",
                table: "PacotesGanhos",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_PacotesGanhos_TreinadorId_AbertoEm",
                table: "PacotesGanhos",
                columns: new[] { "TreinadorId", "AbertoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_PacotesGanhos_TreinadorId_Chave",
                table: "PacotesGanhos",
                columns: new[] { "TreinadorId", "Chave" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FigurinhasDosTreinadores");

            migrationBuilder.DropTable(
                name: "PacotesGanhos");

            migrationBuilder.DropTable(
                name: "Figurinhas");

            migrationBuilder.DropTable(
                name: "Albuns");
        }
    }
}
