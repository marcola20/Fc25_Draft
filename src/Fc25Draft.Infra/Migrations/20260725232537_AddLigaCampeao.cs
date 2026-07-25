using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddLigaCampeao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reconcilia uma divergência pré-existente: a propriedade TransferHistory.CycleId
            // existe no modelo mas nunca teve migração. Idempotente — só cria se faltar.
            migrationBuilder.Sql(
                "ALTER TABLE \"TransferHistories\" ADD COLUMN IF NOT EXISTS \"CycleId\" uuid NULL;");

            migrationBuilder.AddColumn<Guid>(
                name: "CampeaoTimeId",
                table: "Ligas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ligas_CampeaoTimeId",
                table: "Ligas",
                column: "CampeaoTimeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ligas_Teams_CampeaoTimeId",
                table: "Ligas",
                column: "CampeaoTimeId",
                principalTable: "Teams",
                principalColumn: "TeamId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ligas_Teams_CampeaoTimeId",
                table: "Ligas");

            migrationBuilder.DropIndex(
                name: "IX_Ligas_CampeaoTimeId",
                table: "Ligas");

            migrationBuilder.DropColumn(
                name: "CampeaoTimeId",
                table: "Ligas");
        }
    }
}
