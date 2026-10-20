using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AtualizacaoDoAlbum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EntrouEm",
                table: "Figurinhas",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SaiuEm",
                table: "Figurinhas",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Figurinhas_AlbumId_SaiuEm",
                table: "Figurinhas",
                columns: new[] { "AlbumId", "SaiuEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Figurinhas_AlbumId_SaiuEm",
                table: "Figurinhas");

            migrationBuilder.DropColumn(
                name: "EntrouEm",
                table: "Figurinhas");

            migrationBuilder.DropColumn(
                name: "SaiuEm",
                table: "Figurinhas");
        }
    }
}
