using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddLigaCopaPotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LigaCopaPotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LigaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pote = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigaCopaPotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LigaCopaPotes_Ligas_LigaId",
                        column: x => x.LigaId,
                        principalTable: "Ligas",
                        principalColumn: "LigaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LigaCopaPotes_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LigaCopaPotes_LigaId_Pote",
                table: "LigaCopaPotes",
                columns: new[] { "LigaId", "Pote" });

            migrationBuilder.CreateIndex(
                name: "IX_LigaCopaPotes_LigaId_TimeId",
                table: "LigaCopaPotes",
                columns: new[] { "LigaId", "TimeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LigaCopaPotes_TimeId",
                table: "LigaCopaPotes",
                column: "TimeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LigaCopaPotes");
        }
    }
}
