using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddBolaoPalpites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BolaoPalpites",
                columns: table => new
                {
                    PalpiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreinadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartidaId = table.Column<Guid>(type: "uuid", nullable: false),
                    GolsCasa = table.Column<int>(type: "integer", nullable: false),
                    GolsFora = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BolaoPalpites", x => x.PalpiteId);
                    table.ForeignKey(
                        name: "FK_BolaoPalpites_LigaPartidas_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "LigaPartidas",
                        principalColumn: "PartidaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BolaoPalpites_Treinadores_TreinadorId",
                        column: x => x.TreinadorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BolaoPalpites_PartidaId",
                table: "BolaoPalpites",
                column: "PartidaId");

            migrationBuilder.CreateIndex(
                name: "IX_BolaoPalpites_TreinadorId_PartidaId",
                table: "BolaoPalpites",
                columns: new[] { "TreinadorId", "PartidaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BolaoPalpites");
        }
    }
}
