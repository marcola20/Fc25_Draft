using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class TrocasDeFigurinhas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrocasFigurinhas",
                columns: table => new
                {
                    TrocaId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeTreinadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParaTreinadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RespondidaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ContrapropostaDeId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrocasFigurinhas", x => x.TrocaId);
                    table.ForeignKey(
                        name: "FK_TrocasFigurinhas_Albuns_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albuns",
                        principalColumn: "AlbumId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrocasFigurinhas_Treinadores_DeTreinadorId",
                        column: x => x.DeTreinadorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrocasFigurinhas_Treinadores_ParaTreinadorId",
                        column: x => x.ParaTreinadorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrocasFigurinhas_TrocasFigurinhas_ContrapropostaDeId",
                        column: x => x.ContrapropostaDeId,
                        principalTable: "TrocasFigurinhas",
                        principalColumn: "TrocaId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TrocasFigurinhasItens",
                columns: table => new
                {
                    TrocaId = table.Column<Guid>(type: "uuid", nullable: false),
                    FigurinhaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Oferecida = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrocasFigurinhasItens", x => new { x.TrocaId, x.FigurinhaId, x.Oferecida });
                    table.ForeignKey(
                        name: "FK_TrocasFigurinhasItens_Figurinhas_FigurinhaId",
                        column: x => x.FigurinhaId,
                        principalTable: "Figurinhas",
                        principalColumn: "FigurinhaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrocasFigurinhasItens_TrocasFigurinhas_TrocaId",
                        column: x => x.TrocaId,
                        principalTable: "TrocasFigurinhas",
                        principalColumn: "TrocaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrocasFigurinhas_AlbumId",
                table: "TrocasFigurinhas",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_TrocasFigurinhas_ContrapropostaDeId",
                table: "TrocasFigurinhas",
                column: "ContrapropostaDeId");

            migrationBuilder.CreateIndex(
                name: "IX_TrocasFigurinhas_DeTreinadorId_Status",
                table: "TrocasFigurinhas",
                columns: new[] { "DeTreinadorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TrocasFigurinhas_ParaTreinadorId_Status",
                table: "TrocasFigurinhas",
                columns: new[] { "ParaTreinadorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TrocasFigurinhas_Status_ExpiraEm",
                table: "TrocasFigurinhas",
                columns: new[] { "Status", "ExpiraEm" });

            migrationBuilder.CreateIndex(
                name: "IX_TrocasFigurinhasItens_FigurinhaId",
                table: "TrocasFigurinhasItens",
                column: "FigurinhaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrocasFigurinhasItens");

            migrationBuilder.DropTable(
                name: "TrocasFigurinhas");
        }
    }
}
