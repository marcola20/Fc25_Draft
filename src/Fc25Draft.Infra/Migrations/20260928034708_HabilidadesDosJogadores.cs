using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class HabilidadesDosJogadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Condicao",
                table: "PlayerAtributos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstiloDeJogo",
                table: "PlayerAtributos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstilosIa",
                table: "PlayerAtributos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Habilidades",
                table: "PlayerAtributos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeFracoPrecisao",
                table: "PlayerAtributos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeFracoUso",
                table: "PlayerAtributos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Posicoes",
                table: "PlayerAtributos",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResistenciaLesao",
                table: "PlayerAtributos",
                type: "integer",
                nullable: true);

            // Habilidades, estilos, posições etc. dos jogadores que já têm ID do PES (ver .Dados.cs).
            migrationBuilder.Sql(DadosHabilidades);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Condicao",
                table: "PlayerAtributos");

            migrationBuilder.DropColumn(
                name: "EstiloDeJogo",
                table: "PlayerAtributos");

            migrationBuilder.DropColumn(
                name: "EstilosIa",
                table: "PlayerAtributos");

            migrationBuilder.DropColumn(
                name: "Habilidades",
                table: "PlayerAtributos");

            migrationBuilder.DropColumn(
                name: "PeFracoPrecisao",
                table: "PlayerAtributos");

            migrationBuilder.DropColumn(
                name: "PeFracoUso",
                table: "PlayerAtributos");

            migrationBuilder.DropColumn(
                name: "Posicoes",
                table: "PlayerAtributos");

            migrationBuilder.DropColumn(
                name: "ResistenciaLesao",
                table: "PlayerAtributos");
        }
    }
}
