using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PedidoDeSaida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "VariacaoEmBaixa",
                table: "TransferHistories",
                type: "numeric(6,4)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PedidoDeSaidaDesde",
                table: "TransferConfigs",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PromessaEm",
                table: "TeamRosters",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PromessaQuebrada",
                table: "TeamRosters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SaidaPedidaEm",
                table: "TeamRosters",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VariacaoCongelada",
                table: "TeamRosters",
                type: "numeric(6,4)",
                nullable: true);

            // A regra começa agora: os jogos de antes não contam para ninguém pedir para sair.
            migrationBuilder.Sql("""UPDATE "TransferConfigs" SET "PedidoDeSaidaDesde" = timezone('utc', now());""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VariacaoEmBaixa",
                table: "TransferHistories");

            migrationBuilder.DropColumn(
                name: "PedidoDeSaidaDesde",
                table: "TransferConfigs");

            migrationBuilder.DropColumn(
                name: "PromessaEm",
                table: "TeamRosters");

            migrationBuilder.DropColumn(
                name: "PromessaQuebrada",
                table: "TeamRosters");

            migrationBuilder.DropColumn(
                name: "SaidaPedidaEm",
                table: "TeamRosters");

            migrationBuilder.DropColumn(
                name: "VariacaoCongelada",
                table: "TeamRosters");
        }
    }
}
