using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Market_BudgetLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BudgetLedgers",
                columns: table => new
                {
                    BudgetLedgerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Origem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetLedgers", x => x.BudgetLedgerId);
                    table.ForeignKey(
                        name: "FK_BudgetLedgers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.CheckConstraint(
                        name: "CK_BudgetLedger_Tipo",
                        sql: "[Tipo] IN ('CREDIT','DEBIT')");
                    table.CheckConstraint(
                        name: "CK_BudgetLedger_Valor",
                        sql: "[Valor] > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLedger_TeamId_DataUtc",
                table: "BudgetLedgers",
                columns: new[] { "TeamId", "DataUtc" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BudgetLedgers");
        }
    }
}
