using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PES2021_Positions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Positions",
                keyColumn: "PositionId",
                keyValue: (short)3,
                column: "Name",
                value: "Lateral Esquerdo");

            migrationBuilder.UpdateData(
                table: "Positions",
                keyColumn: "PositionId",
                keyValue: (short)4,
                column: "Name",
                value: "Lateral Direito");

            migrationBuilder.UpdateData(
                table: "Positions",
                keyColumn: "PositionId",
                keyValue: (short)6,
                column: "Name",
                value: "Meia de Ligação");

            migrationBuilder.UpdateData(
                table: "Positions",
                keyColumn: "PositionId",
                keyValue: (short)8,
                column: "Name",
                value: "Meia Esquerda");

            migrationBuilder.UpdateData(
                table: "Positions",
                keyColumn: "PositionId",
                keyValue: (short)9,
                column: "Name",
                value: "Ponta Esquerda");

            migrationBuilder.UpdateData(
                table: "Positions",
                keyColumn: "PositionId",
                keyValue: (short)10,
                column: "Name",
                value: "Meia Direita");

            migrationBuilder.InsertData(
                table: "Positions",
                columns: new[] { "PositionId", "Name" },
                values: new object[,]
                {
                    { (short)11, "Ponta Direita" },
                    { (short)12, "Centroavante" },
                    { (short)13, "Segundo Atacante" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Positions",
                keyColumn: "PositionId",
                keyValue: (short)11);

            migrationBuilder.DeleteData(
                table: "Positions",
                keyColumn: "PositionId",
                keyValue: (short)12);

            migrationBuilder.DeleteData(
                table: "Positions",
                keyColumn: "PositionId",
                keyValue: (short)13);

            migrationBuilder.UpdateData(
                table: "Positions",
                keyColumn: "PositionId",
                keyValue: (short)3,
                column: "Name",
                value: "Lateral/Ala Esquerdo");

            migrationBuilder.UpdateData(
                table: "Positions",
                keyColumn: "PositionId",
                keyValue: (short)4,
                column: "Name",
                value: "Lateral/Ala Direito");

            migrationBuilder.UpdateData(
                table: "Positions",
                keyColumn: "PositionId",
                keyValue: (short)6,
                column: "Name",
                value: "Meia Central");

            migrationBuilder.UpdateData(
                table: "Positions",
                keyColumn: "PositionId",
                keyValue: (short)8,
                column: "Name",
                value: "Meia/Ponta Esquerda");

            migrationBuilder.UpdateData(
                table: "Positions",
                keyColumn: "PositionId",
                keyValue: (short)9,
                column: "Name",
                value: "Meia/Ponta Direita");

            migrationBuilder.UpdateData(
                table: "Positions",
                keyColumn: "PositionId",
                keyValue: (short)10,
                column: "Name",
                value: "Atacante");
        }
    }
}
