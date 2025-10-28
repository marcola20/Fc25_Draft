using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PlayerPublicIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "Players",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()");

            migrationBuilder.AddColumn<Guid>(
                name: "PlayerPublicId",
                table: "TransferHistories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE th SET PlayerPublicId = p.PublicId FROM TransferHistories th INNER JOIN Players p ON th.PlayerId = p.PlayerId");

            migrationBuilder.AlterColumn<Guid>(
                name: "PlayerPublicId",
                table: "TransferHistories",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_PublicId",
                table: "Players",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferHistories_PlayerPublic",
                table: "TransferHistories",
                columns: new[] { "PlayerPublicId", "PerformedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferHistories_PlayerPublic",
                table: "TransferHistories");

            migrationBuilder.DropIndex(
                name: "IX_Players_PublicId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PlayerPublicId",
                table: "TransferHistories");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Players");
        }
    }
}
