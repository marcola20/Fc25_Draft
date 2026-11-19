using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PaginasEspeciaisDoAlbum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AlbumConquistas_TreinadorId_AlbumId_Tipo_TeamId",
                table: "AlbumConquistas");

            migrationBuilder.AlterColumn<Guid>(
                name: "TeamId",
                table: "Figurinhas",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "HallOfFameId",
                table: "Figurinhas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Pagina",
                table: "Figurinhas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Pagina",
                table: "AlbumConquistas",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FigurinhasArtes",
                columns: table => new
                {
                    FigurinhaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Imagem = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AtualizadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FigurinhasArtes", x => x.FigurinhaId);
                    table.ForeignKey(
                        name: "FK_FigurinhasArtes_Figurinhas_FigurinhaId",
                        column: x => x.FigurinhaId,
                        principalTable: "Figurinhas",
                        principalColumn: "FigurinhaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Figurinhas_AlbumId_Pagina",
                table: "Figurinhas",
                columns: new[] { "AlbumId", "Pagina" });

            migrationBuilder.CreateIndex(
                name: "IX_AlbumConquistas_TreinadorId_AlbumId_Tipo_TeamId_Pagina",
                table: "AlbumConquistas",
                columns: new[] { "TreinadorId", "AlbumId", "Tipo", "TeamId", "Pagina" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FigurinhasArtes");

            migrationBuilder.DropIndex(
                name: "IX_Figurinhas_AlbumId_Pagina",
                table: "Figurinhas");

            migrationBuilder.DropIndex(
                name: "IX_AlbumConquistas_TreinadorId_AlbumId_Tipo_TeamId_Pagina",
                table: "AlbumConquistas");

            migrationBuilder.DropColumn(
                name: "HallOfFameId",
                table: "Figurinhas");

            migrationBuilder.DropColumn(
                name: "Pagina",
                table: "Figurinhas");

            migrationBuilder.DropColumn(
                name: "Pagina",
                table: "AlbumConquistas");

            migrationBuilder.AlterColumn<Guid>(
                name: "TeamId",
                table: "Figurinhas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlbumConquistas_TreinadorId_AlbumId_Tipo_TeamId",
                table: "AlbumConquistas",
                columns: new[] { "TreinadorId", "AlbumId", "Tipo", "TeamId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}
