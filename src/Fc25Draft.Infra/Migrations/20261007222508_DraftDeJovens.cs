using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class DraftDeJovens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdadeMaximaDraft",
                table: "TransferConfigs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdadeMaxima",
                table: "Drafts",
                type: "integer",
                nullable: true);

            // O draft passa a ser de jovens (decisão de 07/10/2026): a configuração que já existe começa em 23.
            migrationBuilder.Sql("UPDATE \"TransferConfigs\" SET \"IdadeMaximaDraft\" = 23;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdadeMaximaDraft",
                table: "TransferConfigs");

            migrationBuilder.DropColumn(
                name: "IdadeMaxima",
                table: "Drafts");
        }
    }
}
