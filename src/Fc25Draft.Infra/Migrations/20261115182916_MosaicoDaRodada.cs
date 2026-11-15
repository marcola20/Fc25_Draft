using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class MosaicoDaRodada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "PartidaId",
                table: "PartidaChatMensagens",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "MosaicoId",
                table: "PartidaChatMensagens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Mosaicos",
                columns: table => new
                {
                    MosaicoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    YoutubeVideoId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mosaicos", x => x.MosaicoId);
                });

            migrationBuilder.CreateTable(
                name: "MosaicoReacoes",
                columns: table => new
                {
                    MosaicoId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreinadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Emoji = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MosaicoReacoes", x => new { x.MosaicoId, x.TreinadorId, x.Emoji });
                    table.ForeignKey(
                        name: "FK_MosaicoReacoes_Mosaicos_MosaicoId",
                        column: x => x.MosaicoId,
                        principalTable: "Mosaicos",
                        principalColumn: "MosaicoId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MosaicoReacoes_Treinadores_TreinadorId",
                        column: x => x.TreinadorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MosaicoRodadas",
                columns: table => new
                {
                    MosaicoId = table.Column<Guid>(type: "uuid", nullable: false),
                    RodadaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MosaicoRodadas", x => new { x.MosaicoId, x.RodadaId });
                    table.ForeignKey(
                        name: "FK_MosaicoRodadas_LigaRodadas_RodadaId",
                        column: x => x.RodadaId,
                        principalTable: "LigaRodadas",
                        principalColumn: "RodadaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MosaicoRodadas_Mosaicos_MosaicoId",
                        column: x => x.MosaicoId,
                        principalTable: "Mosaicos",
                        principalColumn: "MosaicoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartidaChatMensagens_MosaicoId_EnviadaEm",
                table: "PartidaChatMensagens",
                columns: new[] { "MosaicoId", "EnviadaEm" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartidaChatMensagens_Sala",
                table: "PartidaChatMensagens",
                sql: "(\"PartidaId\" IS NULL) <> (\"MosaicoId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_MosaicoReacoes_TreinadorId",
                table: "MosaicoReacoes",
                column: "TreinadorId");

            migrationBuilder.CreateIndex(
                name: "IX_MosaicoRodadas_RodadaId",
                table: "MosaicoRodadas",
                column: "RodadaId");

            migrationBuilder.CreateIndex(
                name: "IX_Mosaicos_CriadoEm",
                table: "Mosaicos",
                column: "CriadoEm");

            migrationBuilder.AddForeignKey(
                name: "FK_PartidaChatMensagens_Mosaicos_MosaicoId",
                table: "PartidaChatMensagens",
                column: "MosaicoId",
                principalTable: "Mosaicos",
                principalColumn: "MosaicoId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartidaChatMensagens_Mosaicos_MosaicoId",
                table: "PartidaChatMensagens");

            migrationBuilder.DropTable(
                name: "MosaicoReacoes");

            migrationBuilder.DropTable(
                name: "MosaicoRodadas");

            migrationBuilder.DropTable(
                name: "Mosaicos");

            migrationBuilder.DropIndex(
                name: "IX_PartidaChatMensagens_MosaicoId_EnviadaEm",
                table: "PartidaChatMensagens");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PartidaChatMensagens_Sala",
                table: "PartidaChatMensagens");

            migrationBuilder.DropColumn(
                name: "MosaicoId",
                table: "PartidaChatMensagens");

            migrationBuilder.AlterColumn<Guid>(
                name: "PartidaId",
                table: "PartidaChatMensagens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
