using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AberturaDaTemporada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AberturasTemporada",
                columns: table => new
                {
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    Abertura = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AberturasTemporada", x => x.Temporada);
                });

            // A temporada em andamento abriu no domingo 27/09/2026 (a data que ficava fixa no código).
            migrationBuilder.InsertData(
                table: "AberturasTemporada",
                columns: new[] { "Temporada", "Abertura", "AtualizadoEm" },
                values: new object[] { 2010, new DateTime(2026, 9, 27), new DateTime(2026, 10, 1) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AberturasTemporada");
        }
    }
}
