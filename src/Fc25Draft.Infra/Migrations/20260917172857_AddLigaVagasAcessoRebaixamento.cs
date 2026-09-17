using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddLigaVagasAcessoRebaixamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VagasDiretas",
                table: "Ligas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VagasPlayoff",
                table: "Ligas",
                type: "integer",
                nullable: true);

            // Temporada em andamento (Série A de 12 times): os 2 últimos caem direto para a Série B,
            // sem playoff, porque a próxima Série A terá 10 times. Temporadas encerradas ficam sem zonas.
            migrationBuilder.Sql("""
                UPDATE "Ligas"
                SET "VagasDiretas" = 2, "VagasPlayoff" = 0
                WHERE "Tipo" = 0 AND "Divisao" = 1 AND "Status" <> 4 AND "Temporada" = 2009;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VagasDiretas",
                table: "Ligas");

            migrationBuilder.DropColumn(
                name: "VagasPlayoff",
                table: "Ligas");
        }
    }
}
