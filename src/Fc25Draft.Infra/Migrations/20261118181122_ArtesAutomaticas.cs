using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class ArtesAutomaticas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArtesConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Ligado = table.Column<bool>(type: "boolean", nullable: false),
                    ValorMinimo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OverMinimo = table.Column<int>(type: "integer", nullable: false),
                    TransferenciasDesdeUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtesConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PedidosArte",
                columns: table => new
                {
                    PedidoArteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Chave = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    ReferenciaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Situacao = table.Column<int>(type: "integer", nullable: false),
                    Tentativas = table.Column<int>(type: "integer", nullable: false),
                    Erro = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EntregueEmUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConcluidoEmUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidosArte", x => x.PedidoArteId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PedidosArte_Chave",
                table: "PedidosArte",
                column: "Chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PedidosArte_Situacao_CriadoEmUtc",
                table: "PedidosArte",
                columns: new[] { "Situacao", "CriadoEmUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtesConfig");

            migrationBuilder.DropTable(
                name: "PedidosArte");
        }
    }
}
