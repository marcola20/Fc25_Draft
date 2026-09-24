using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftRelogio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PausadoEm",
                table: "Drafts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TempoPorEscolhaMinutos",
                table: "Drafts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VezIniciadaEm",
                table: "Drafts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TempoEsgotado",
                table: "DraftPicks",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PausadoEm",
                table: "Drafts");

            migrationBuilder.DropColumn(
                name: "TempoPorEscolhaMinutos",
                table: "Drafts");

            migrationBuilder.DropColumn(
                name: "VezIniciadaEm",
                table: "Drafts");

            migrationBuilder.DropColumn(
                name: "TempoEsgotado",
                table: "DraftPicks");
        }
    }
}
