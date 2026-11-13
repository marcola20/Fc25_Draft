using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AnunciosNoSite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PublicadoEm",
                table: "Anuncios",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Resumo",
                table: "Anuncios",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TextoSite",
                table: "Anuncios",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Anuncios_PublicadoEm",
                table: "Anuncios",
                column: "PublicadoEm");

            PreencherTextosDoSite(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Anuncios_PublicadoEm",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "PublicadoEm",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "Resumo",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "TextoSite",
                table: "Anuncios");
        }
    }
}
