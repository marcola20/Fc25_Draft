using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class EvolucoesDeAtributosPes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PosicaoPes",
                table: "PlayerAtributos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EvolucoesPes",
                columns: table => new
                {
                    EvolucaoPesId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CriadaEmUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    OverallAntes = table.Column<int>(type: "integer", nullable: false),
                    OverallDepois = table.Column<int>(type: "integer", nullable: false),
                    Mudancas = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AplicadaNoJogoEmUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvolucoesPes", x => x.EvolucaoPesId);
                    table.ForeignKey(
                        name: "FK_EvolucoesPes_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvolucoesPes_AplicadaNoJogoEmUtc",
                table: "EvolucoesPes",
                column: "AplicadaNoJogoEmUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EvolucoesPes_PlayerId",
                table: "EvolucoesPes",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvolucoesPes");

            migrationBuilder.DropColumn(
                name: "PosicaoPes",
                table: "PlayerAtributos");
        }
    }
}
