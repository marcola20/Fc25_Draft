using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class EvolucaoNaVendaRapida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvolucoesVendaRapida",
                columns: table => new
                {
                    Overall = table.Column<int>(type: "integer", nullable: false),
                    Minimo = table.Column<int>(type: "integer", nullable: false),
                    Maximo = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvolucoesVendaRapida", x => x.Overall);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvolucoesVendaRapida");
        }
    }
}
