using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Aposentadoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AposentadoEm",
                table: "Players",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AposentadoNaTemporada",
                table: "Players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DespedidaAnunciadaEm",
                table: "Players",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TimeAoSeAposentar",
                table: "Players",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UltimaTemporada",
                table: "Players",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AposentadoEm",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "AposentadoNaTemporada",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DespedidaAnunciadaEm",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "TimeAoSeAposentar",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "UltimaTemporada",
                table: "Players");
        }
    }
}
