using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class TelaoDoJogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "YoutubeVideoId",
                table: "LigaPartidas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PartidaChatMensagens",
                columns: table => new
                {
                    MensagemId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartidaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreinadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Texto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    EnviadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartidaChatMensagens", x => x.MensagemId);
                    table.ForeignKey(
                        name: "FK_PartidaChatMensagens_LigaPartidas_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "LigaPartidas",
                        principalColumn: "PartidaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartidaChatMensagens_Treinadores_TreinadorId",
                        column: x => x.TreinadorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartidaChatMensagens_PartidaId_EnviadaEm",
                table: "PartidaChatMensagens",
                columns: new[] { "PartidaId", "EnviadaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_PartidaChatMensagens_TreinadorId",
                table: "PartidaChatMensagens",
                column: "TreinadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartidaChatMensagens");

            migrationBuilder.DropColumn(
                name: "YoutubeVideoId",
                table: "LigaPartidas");
        }
    }
}
