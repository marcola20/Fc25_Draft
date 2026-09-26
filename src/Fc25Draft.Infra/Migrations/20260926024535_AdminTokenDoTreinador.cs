using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AdminTokenDoTreinador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TreinadorId",
                table: "AdminTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminTokens_TreinadorId",
                table: "AdminTokens",
                column: "TreinadorId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdminTokens_Treinadores_TreinadorId",
                table: "AdminTokens",
                column: "TreinadorId",
                principalTable: "Treinadores",
                principalColumn: "TreinadorId",
                onDelete: ReferentialAction.SetNull);

            // O admin da liga também é uma pessoa: liga o token dele ao cadastro de mesmo nome
            // para ele poder palpitar no bolão sem trocar de login.
            migrationBuilder.Sql(@"
                UPDATE ""AdminTokens"" a
                   SET ""TreinadorId"" = t.""TreinadorId""
                  FROM ""Treinadores"" t
                 WHERE a.""TreinadorId"" IS NULL
                   AND a.""Description"" IS NOT NULL
                   AND lower(btrim(a.""Description"")) = lower(btrim(t.""Nome""));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdminTokens_Treinadores_TreinadorId",
                table: "AdminTokens");

            migrationBuilder.DropIndex(
                name: "IX_AdminTokens_TreinadorId",
                table: "AdminTokens");

            migrationBuilder.DropColumn(
                name: "TreinadorId",
                table: "AdminTokens");
        }
    }
}
