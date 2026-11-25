using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class IdolosDosClubes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Idolos",
                columns: table => new
                {
                    IdoloId = table.Column<Guid>(type: "uuid", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Jogos = table.Column<int>(type: "integer", nullable: false),
                    Gols = table.Column<int>(type: "integer", nullable: false),
                    Assistencias = table.Column<int>(type: "integer", nullable: false),
                    Titulos = table.Column<int>(type: "integer", nullable: false),
                    Motivos = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReconhecidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SaiuEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Saida = table.Column<int>(type: "integer", nullable: true),
                    PerdeuConfianca = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Idolos", x => x.IdoloId);
                    table.ForeignKey(
                        name: "FK_Idolos_Players_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Idolos_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Idolos_JogadorId_TimeId",
                table: "Idolos",
                columns: new[] { "JogadorId", "TimeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Idolos_TimeId",
                table: "Idolos",
                column: "TimeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Idolos");
        }
    }
}
