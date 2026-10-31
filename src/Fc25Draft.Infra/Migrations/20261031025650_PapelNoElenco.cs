using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PapelNoElenco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PapelNoElencoDesde",
                table: "TransferConfigs",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConversaEm",
                table: "Teams",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConversaEm",
                table: "TeamRosters",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "Papel",
                table: "TeamRosters",
                type: "smallint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PapelNoElencoDesde",
                table: "TransferConfigs");

            migrationBuilder.DropColumn(
                name: "ConversaEm",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "ConversaEm",
                table: "TeamRosters");

            migrationBuilder.DropColumn(
                name: "Papel",
                table: "TeamRosters");
        }
    }
}
