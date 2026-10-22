using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class ResultadosPesPendentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResultadosPesConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Segurar = table.Column<bool>(type: "boolean", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultadosPesConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResultadosPesPendentes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Video = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Json = table.Column<string>(type: "jsonb", nullable: false),
                    PartidaId = table.Column<Guid>(type: "uuid", nullable: true),
                    KnockoutJogoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Competicao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Onde = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Casa = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Fora = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GolsCasa = table.Column<int>(type: "integer", nullable: false),
                    GolsFora = table.Column<int>(type: "integer", nullable: false),
                    DataHora = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Avisos = table.Column<string>(type: "text", nullable: true),
                    Erro = table.Column<string>(type: "text", nullable: true),
                    EnviadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultadosPesPendentes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResultadosPesConfig");

            migrationBuilder.DropTable(
                name: "ResultadosPesPendentes");
        }
    }
}
