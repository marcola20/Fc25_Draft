using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamLineupRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CaptainPlayerId",
                table: "TeamLineups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CornerLeftPlayerId",
                table: "TeamLineups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CornerRightPlayerId",
                table: "TeamLineups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LongFreeKickPlayerId",
                table: "TeamLineups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PenaltiesPlayerId",
                table: "TeamLineups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShortFreeKickLeftPlayerId",
                table: "TeamLineups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShortFreeKickRightPlayerId",
                table: "TeamLineups",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineups_CaptainPlayerId",
                table: "TeamLineups",
                column: "CaptainPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineups_CornerLeftPlayerId",
                table: "TeamLineups",
                column: "CornerLeftPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineups_CornerRightPlayerId",
                table: "TeamLineups",
                column: "CornerRightPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineups_LongFreeKickPlayerId",
                table: "TeamLineups",
                column: "LongFreeKickPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineups_PenaltiesPlayerId",
                table: "TeamLineups",
                column: "PenaltiesPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineups_ShortFreeKickLeftPlayerId",
                table: "TeamLineups",
                column: "ShortFreeKickLeftPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineups_ShortFreeKickRightPlayerId",
                table: "TeamLineups",
                column: "ShortFreeKickRightPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeamLineups_Players_CaptainPlayerId",
                table: "TeamLineups",
                column: "CaptainPlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamLineups_Players_CornerLeftPlayerId",
                table: "TeamLineups",
                column: "CornerLeftPlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamLineups_Players_CornerRightPlayerId",
                table: "TeamLineups",
                column: "CornerRightPlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamLineups_Players_LongFreeKickPlayerId",
                table: "TeamLineups",
                column: "LongFreeKickPlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamLineups_Players_PenaltiesPlayerId",
                table: "TeamLineups",
                column: "PenaltiesPlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamLineups_Players_ShortFreeKickLeftPlayerId",
                table: "TeamLineups",
                column: "ShortFreeKickLeftPlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamLineups_Players_ShortFreeKickRightPlayerId",
                table: "TeamLineups",
                column: "ShortFreeKickRightPlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamLineups_Players_CaptainPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamLineups_Players_CornerLeftPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamLineups_Players_CornerRightPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamLineups_Players_LongFreeKickPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamLineups_Players_PenaltiesPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamLineups_Players_ShortFreeKickLeftPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamLineups_Players_ShortFreeKickRightPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropIndex(
                name: "IX_TeamLineups_CaptainPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropIndex(
                name: "IX_TeamLineups_CornerLeftPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropIndex(
                name: "IX_TeamLineups_CornerRightPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropIndex(
                name: "IX_TeamLineups_LongFreeKickPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropIndex(
                name: "IX_TeamLineups_PenaltiesPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropIndex(
                name: "IX_TeamLineups_ShortFreeKickLeftPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropIndex(
                name: "IX_TeamLineups_ShortFreeKickRightPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropColumn(
                name: "CaptainPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropColumn(
                name: "CornerLeftPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropColumn(
                name: "CornerRightPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropColumn(
                name: "LongFreeKickPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropColumn(
                name: "PenaltiesPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropColumn(
                name: "ShortFreeKickLeftPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropColumn(
                name: "ShortFreeKickRightPlayerId",
                table: "TeamLineups");
        }
    }
}
