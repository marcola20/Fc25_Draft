using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftWishlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DraftWishlistEntries",
                columns: table => new
                {
                    DraftWishlistEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftWishlistEntries", x => x.DraftWishlistEntryId);
                    table.ForeignKey(
                        name: "FK_DraftWishlistEntries_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DraftWishlistEntries_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DraftWishlistEntries_PlayerId",
                table: "DraftWishlistEntries",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftWishlistEntries_TeamId",
                table: "DraftWishlistEntries",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftWishlistEntries_TeamId_PlayerId",
                table: "DraftWishlistEntries",
                columns: new[] { "TeamId", "PlayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DraftWishlistEntries");
        }
    }
}
