using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddHallOfFameTreinador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TreinadorId",
                table: "HallOfFame",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HallOfFame_TreinadorId",
                table: "HallOfFame",
                column: "TreinadorId");

            // Quem já estava no Hall of Fame com o nome escrito à mão passa a apontar
            // para o cadastro, desde que só exista um treinador com aquele nome.
            migrationBuilder.Sql(@"
                UPDATE ""HallOfFame"" h
                   SET ""TreinadorId"" = (
                        SELECT t.""TreinadorId"" FROM ""Treinadores"" t
                         WHERE lower(trim(t.""Nome"")) = lower(trim(h.""Tecnico""))
                         LIMIT 1)
                 WHERE h.""TreinadorId"" IS NULL
                   AND h.""Tecnico"" IS NOT NULL
                   AND (SELECT count(*) FROM ""Treinadores"" t
                         WHERE lower(trim(t.""Nome"")) = lower(trim(h.""Tecnico""))) = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HallOfFame_TreinadorId",
                table: "HallOfFame");

            migrationBuilder.DropColumn(
                name: "TreinadorId",
                table: "HallOfFame");
        }
    }
}
