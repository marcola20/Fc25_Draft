using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    BaseScale = table.Column<decimal>(type: "numeric", nullable: false),
                    OverallBase = table.Column<decimal>(type: "numeric", nullable: false),
                    OverallPivot = table.Column<int>(type: "integer", nullable: false),
                    BuyNowFactor = table.Column<decimal>(type: "numeric", nullable: false),
                    MinIncrementRate = table.Column<decimal>(type: "numeric", nullable: false),
                    MinIncrementStep = table.Column<decimal>(type: "numeric", nullable: false),
                    AgeFactorUpTo22 = table.Column<decimal>(type: "numeric", nullable: false),
                    AgeFactor23To24 = table.Column<decimal>(type: "numeric", nullable: false),
                    AgeFactor25To26 = table.Column<decimal>(type: "numeric", nullable: false),
                    AgeFactor27To28 = table.Column<decimal>(type: "numeric", nullable: false),
                    AgeFactor29To30 = table.Column<decimal>(type: "numeric", nullable: false),
                    AgeFactor31To32 = table.Column<decimal>(type: "numeric", nullable: false),
                    AgeFactor33To34 = table.Column<decimal>(type: "numeric", nullable: false),
                    AgeFactor35Plus = table.Column<decimal>(type: "numeric", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingConfigs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PricingConfigs");
        }
    }
}
