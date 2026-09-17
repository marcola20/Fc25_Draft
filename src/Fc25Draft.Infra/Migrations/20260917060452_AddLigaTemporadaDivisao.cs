using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddLigaTemporadaDivisao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Divisao",
                table: "Ligas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Temporada",
                table: "Ligas",
                type: "integer",
                nullable: true);

            // Ligas existentes: temporada = ano no nome ("Brasileirão CBFV 2009" → 2009).
            migrationBuilder.Sql("""
                UPDATE "Ligas"
                SET "Temporada" = substring("Nome" from '((19|2[0-9])[0-9]{2})')::int
                WHERE "Nome" ~ '(19|2[0-9])[0-9]{2}';
                """);

            // Antes só existia uma divisão: Ligas de pontos corridos viram Série A,
            // exceto se houver duas na mesma temporada (ficam sem divisão para o admin decidir).
            migrationBuilder.Sql("""
                UPDATE "Ligas" l
                SET "Divisao" = 1
                WHERE l."Tipo" = 0
                  AND l."Temporada" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM "Ligas" o
                      WHERE o."Tipo" = 0 AND o."Temporada" = l."Temporada" AND o."LigaId" <> l."LigaId");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Ligas_Temporada_Divisao",
                table: "Ligas",
                columns: new[] { "Temporada", "Divisao" },
                unique: true,
                filter: "\"Temporada\" IS NOT NULL AND \"Divisao\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ligas_Temporada_Divisao",
                table: "Ligas");

            migrationBuilder.DropColumn(
                name: "Divisao",
                table: "Ligas");

            migrationBuilder.DropColumn(
                name: "Temporada",
                table: "Ligas");
        }
    }
}
