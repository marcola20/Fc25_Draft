using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Emprestimos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BuyOptionPrice",
                table: "TransferOffers",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxLoans",
                table: "TransferConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "LoanCount",
                table: "Teams",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Emprestimos",
                columns: table => new
                {
                    EmprestimoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    DonoTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    TomadorTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValorOpcaoCompra = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FimUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Emprestimos", x => x.EmprestimoId);
                    table.ForeignKey(
                        name: "FK_Emprestimos_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Emprestimos_Teams_DonoTeamId",
                        column: x => x.DonoTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_Emprestimos_Teams_TomadorTeamId",
                        column: x => x.TomadorTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Emprestimos_DonoTeamId",
                table: "Emprestimos",
                column: "DonoTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Emprestimos_PlayerId",
                table: "Emprestimos",
                column: "PlayerId",
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Emprestimos_TomadorTeamId",
                table: "Emprestimos",
                column: "TomadorTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Emprestimos");

            migrationBuilder.DropColumn(
                name: "BuyOptionPrice",
                table: "TransferOffers");

            migrationBuilder.DropColumn(
                name: "MaxLoans",
                table: "TransferConfigs");

            migrationBuilder.DropColumn(
                name: "LoanCount",
                table: "Teams");
        }
    }
}
