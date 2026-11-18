using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PremiosEstatisticaPorCompeticao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LigaId",
                table: "PremiosEstatistica",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PremiosEstatistica_LigaId_Categoria",
                table: "PremiosEstatistica",
                columns: new[] { "LigaId", "Categoria" });

            migrationBuilder.AddForeignKey(
                name: "FK_PremiosEstatistica_Ligas_LigaId",
                table: "PremiosEstatistica",
                column: "LigaId",
                principalTable: "Ligas",
                principalColumn: "LigaId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PremiosEstatistica_Ligas_LigaId",
                table: "PremiosEstatistica");

            migrationBuilder.DropIndex(
                name: "IX_PremiosEstatistica_LigaId_Categoria",
                table: "PremiosEstatistica");

            migrationBuilder.DropColumn(
                name: "LigaId",
                table: "PremiosEstatistica");
        }
    }
}
