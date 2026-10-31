using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PicksProtegidas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Compensacao",
                table: "TransferOfferPicks",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProtegidaAte",
                table: "TransferOfferPicks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Compensacao",
                table: "PicksDoDraft",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProtecaoAcionada",
                table: "PicksDoDraft",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProtecaoResolvidaEm",
                table: "PicksDoDraft",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProtegidaAte",
                table: "PicksDoDraft",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProtetorId",
                table: "PicksDoDraft",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Temporada",
                table: "Drafts",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Compensacao",
                table: "TransferOfferPicks");

            migrationBuilder.DropColumn(
                name: "ProtegidaAte",
                table: "TransferOfferPicks");

            migrationBuilder.DropColumn(
                name: "Compensacao",
                table: "PicksDoDraft");

            migrationBuilder.DropColumn(
                name: "ProtecaoAcionada",
                table: "PicksDoDraft");

            migrationBuilder.DropColumn(
                name: "ProtecaoResolvidaEm",
                table: "PicksDoDraft");

            migrationBuilder.DropColumn(
                name: "ProtegidaAte",
                table: "PicksDoDraft");

            migrationBuilder.DropColumn(
                name: "ProtetorId",
                table: "PicksDoDraft");

            migrationBuilder.DropColumn(
                name: "Temporada",
                table: "Drafts");
        }
    }
}
