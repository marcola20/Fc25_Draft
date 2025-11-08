using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerQuickSellMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "QuickSellTeamId",
                table: "Players",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuickSellOldOverall",
                table: "Players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuickSellNewOverall",
                table: "Players",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuickSellTeamId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "QuickSellOldOverall",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "QuickSellNewOverall",
                table: "Players");
        }
    }
}
