using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class ClausulaDeRecompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BuybackPrice",
                table: "TransferOffers",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DireitosDeRecompra",
                columns: table => new
                {
                    DireitoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    VendedorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompradorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Preco = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Temporada = table.Column<int>(type: "integer", nullable: true),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExercidaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DireitosDeRecompra", x => x.DireitoId);
                    table.ForeignKey(
                        name: "FK_DireitosDeRecompra_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DireitosDeRecompra_Teams_CompradorId",
                        column: x => x.CompradorId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DireitosDeRecompra_Teams_VendedorId",
                        column: x => x.VendedorId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DireitosDeRecompra_CompradorId",
                table: "DireitosDeRecompra",
                column: "CompradorId");

            migrationBuilder.CreateIndex(
                name: "IX_DireitosDeRecompra_PlayerId",
                table: "DireitosDeRecompra",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_DireitosDeRecompra_VendedorId",
                table: "DireitosDeRecompra",
                column: "VendedorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DireitosDeRecompra");

            migrationBuilder.DropColumn(
                name: "BuybackPrice",
                table: "TransferOffers");
        }
    }
}
