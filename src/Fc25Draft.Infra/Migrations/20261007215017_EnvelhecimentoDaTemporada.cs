using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class EnvelhecimentoDaTemporada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnvelhecimentosTemporada",
                columns: table => new
                {
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    AplicadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Jogadores = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvelhecimentosTemporada", x => x.Temporada);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnvelhecimentosTemporada");
        }
    }
}
