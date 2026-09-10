using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftWishlistEdicoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DraftWishlistEntries_TeamId_PlayerId",
                table: "DraftWishlistEntries");

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "DraftWishlistEntries",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "DraftWishlistEdicoes",
                columns: table => new
                {
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EncerradoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftWishlistEdicoes", x => x.Numero);
                });

            // As listas já enviadas passam a ser a "Versão 1", preservadas como estão. Ela continua
            // aberta até o admin abrir a próxima versão. O menor CriadoEm das entradas vira a data
            // de criação; se não houver nenhuma, usa agora.
            migrationBuilder.Sql("""
                INSERT INTO "DraftWishlistEdicoes" ("Numero", "Nome", "CriadoEm", "EncerradoEm")
                SELECT 1,
                       'Versão 1',
                       COALESCE((SELECT MIN("CriadoEm") FROM "DraftWishlistEntries"), NOW() AT TIME ZONE 'UTC'),
                       NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DraftWishlistEntries_Versao_TeamId",
                table: "DraftWishlistEntries",
                columns: new[] { "Versao", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_DraftWishlistEntries_Versao_TeamId_PlayerId",
                table: "DraftWishlistEntries",
                columns: new[] { "Versao", "TeamId", "PlayerId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DraftWishlistEntries_DraftWishlistEdicoes_Versao",
                table: "DraftWishlistEntries",
                column: "Versao",
                principalTable: "DraftWishlistEdicoes",
                principalColumn: "Numero",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DraftWishlistEntries_DraftWishlistEdicoes_Versao",
                table: "DraftWishlistEntries");

            migrationBuilder.DropTable(
                name: "DraftWishlistEdicoes");

            migrationBuilder.DropIndex(
                name: "IX_DraftWishlistEntries_Versao_TeamId",
                table: "DraftWishlistEntries");

            migrationBuilder.DropIndex(
                name: "IX_DraftWishlistEntries_Versao_TeamId_PlayerId",
                table: "DraftWishlistEntries");

            // Sem o conceito de versão só a Versão 1 cabe no índice único antigo (TeamId, PlayerId).
            migrationBuilder.Sql("""DELETE FROM "DraftWishlistEntries" WHERE "Versao" <> 1;""");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "DraftWishlistEntries");

            migrationBuilder.CreateIndex(
                name: "IX_DraftWishlistEntries_TeamId_PlayerId",
                table: "DraftWishlistEntries",
                columns: new[] { "TeamId", "PlayerId" },
                unique: true);
        }
    }
}
