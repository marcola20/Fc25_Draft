using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddHallOfFame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HallOfFame",
                columns: table => new
                {
                    HallOfFameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TimeCampeao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Ano = table.Column<int>(type: "integer", nullable: true),
                    Temporada = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallOfFame", x => x.HallOfFameId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HallOfFame");
        }
    }
}
