using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddLigaPartidaImportacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LigaPartidaImportacoes",
                columns: table => new
                {
                    PartidaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Video = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Json = table.Column<string>(type: "jsonb", nullable: false),
                    ImportadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigaPartidaImportacoes", x => x.PartidaId);
                    table.ForeignKey(
                        name: "FK_LigaPartidaImportacoes_LigaPartidas_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "LigaPartidas",
                        principalColumn: "PartidaId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LigaPartidaImportacoes");
        }
    }
}
