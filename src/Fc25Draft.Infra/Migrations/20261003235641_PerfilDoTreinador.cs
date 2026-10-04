using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PerfilDoTreinador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Papel",
                table: "Figurinhas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TreinadorId",
                table: "Figurinhas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PerfisTreinadores",
                columns: table => new
                {
                    TreinadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Apelido = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Frase = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Esquema = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    Imagem = table.Column<byte[]>(type: "bytea", nullable: true),
                    ContentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    FotoAtualizadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerfisTreinadores", x => x.TreinadorId);
                    table.ForeignKey(
                        name: "FK_PerfisTreinadores_Treinadores_TreinadorId",
                        column: x => x.TreinadorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Figurinhas_TreinadorId",
                table: "Figurinhas",
                column: "TreinadorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Figurinhas_Treinadores_TreinadorId",
                table: "Figurinhas",
                column: "TreinadorId",
                principalTable: "Treinadores",
                principalColumn: "TreinadorId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Figurinhas_Treinadores_TreinadorId",
                table: "Figurinhas");

            migrationBuilder.DropTable(
                name: "PerfisTreinadores");

            migrationBuilder.DropIndex(
                name: "IX_Figurinhas_TreinadorId",
                table: "Figurinhas");

            migrationBuilder.DropColumn(
                name: "Papel",
                table: "Figurinhas");

            migrationBuilder.DropColumn(
                name: "TreinadorId",
                table: "Figurinhas");
        }
    }
}
