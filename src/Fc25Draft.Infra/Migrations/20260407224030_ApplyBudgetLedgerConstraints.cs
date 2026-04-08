using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class ApplyBudgetLedgerConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetLedgers_Teams_TeamId",
                table: "BudgetLedgers");

            migrationBuilder.DropIndex(
                name: "IX_BudgetLedgers_TeamId",
                table: "BudgetLedgers");

            migrationBuilder.AlterColumn<decimal>(
                name: "Valor",
                table: "BudgetLedgers",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Tipo",
                table: "BudgetLedgers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Origem",
                table: "BudgetLedgers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Descricao",
                table: "BudgetLedgers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLedger_TeamId_DataUtc",
                table: "BudgetLedgers",
                columns: new[] { "TeamId", "DataUtc" },
                descending: new[] { false, true });

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetLedger_Tipo",
                table: "BudgetLedgers",
                sql: "\"Tipo\" IN ('CREDIT','DEBIT')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetLedger_Valor",
                table: "BudgetLedgers",
                sql: "\"Valor\" > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetLedgers_Teams_TeamId",
                table: "BudgetLedgers",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "TeamId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetLedgers_Teams_TeamId",
                table: "BudgetLedgers");

            migrationBuilder.DropIndex(
                name: "IX_BudgetLedger_TeamId_DataUtc",
                table: "BudgetLedgers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetLedger_Tipo",
                table: "BudgetLedgers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetLedger_Valor",
                table: "BudgetLedgers");

            migrationBuilder.AlterColumn<decimal>(
                name: "Valor",
                table: "BudgetLedgers",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "Tipo",
                table: "BudgetLedgers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "Origem",
                table: "BudgetLedgers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Descricao",
                table: "BudgetLedgers",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLedgers_TeamId",
                table: "BudgetLedgers",
                column: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetLedgers_Teams_TeamId",
                table: "BudgetLedgers",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "TeamId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
