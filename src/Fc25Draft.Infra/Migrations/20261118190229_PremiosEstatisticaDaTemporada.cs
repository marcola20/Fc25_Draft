using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PremiosEstatisticaDaTemporada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PremiosEstatistica_Ligas_LigaId",
                table: "PremiosEstatistica");

            migrationBuilder.DropIndex(
                name: "IX_PremiosEstatistica_LigaId_Categoria_PlayerId",
                table: "PremiosEstatistica");

            migrationBuilder.DropIndex(
                name: "IX_PremiosEstatistica_Temporada",
                table: "PremiosEstatistica");

            migrationBuilder.DropColumn(
                name: "LigaId",
                table: "PremiosEstatistica");

            migrationBuilder.AddColumn<string>(
                name: "Detalhe",
                table: "PremiosEstatistica",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PremiosEstatistica_Temporada_Categoria",
                table: "PremiosEstatistica",
                columns: new[] { "Temporada", "Categoria" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PremiosEstatistica_Temporada_Categoria",
                table: "PremiosEstatistica");

            migrationBuilder.DropColumn(
                name: "Detalhe",
                table: "PremiosEstatistica");

            migrationBuilder.AddColumn<Guid>(
                name: "LigaId",
                table: "PremiosEstatistica",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_PremiosEstatistica_LigaId_Categoria_PlayerId",
                table: "PremiosEstatistica",
                columns: new[] { "LigaId", "Categoria", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PremiosEstatistica_Temporada",
                table: "PremiosEstatistica",
                column: "Temporada");

            migrationBuilder.AddForeignKey(
                name: "FK_PremiosEstatistica_Ligas_LigaId",
                table: "PremiosEstatistica",
                column: "LigaId",
                principalTable: "Ligas",
                principalColumn: "LigaId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
