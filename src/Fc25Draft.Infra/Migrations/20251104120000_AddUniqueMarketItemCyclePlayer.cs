using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueMarketItemCyclePlayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
WITH duplicates AS (
    SELECT "ItemId"
    FROM (
        SELECT "ItemId",
               ROW_NUMBER() OVER (PARTITION BY "CycleId", "PlayerId" ORDER BY "CreatedAtUtc" DESC, "ItemId") AS rn
        FROM "MarketItems"
        WHERE "PlayerId" IS NOT NULL
    ) ranked
    WHERE rn > 1
)
DELETE FROM "MarketItems" mi
USING duplicates d
WHERE mi."ItemId" = d."ItemId";
""");

            migrationBuilder.CreateIndex(
                name: "IX_MarketItems_CycleId_PlayerId",
                table: "MarketItems",
                columns: new[] { "CycleId", "PlayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketItems_CycleId_PlayerId",
                table: "MarketItems");
        }
    }
}
