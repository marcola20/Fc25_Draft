using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTeamLineupsMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "TeamLineups",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "Escalação");

            migrationBuilder.AddColumn<string>(
                name: "Observation",
                table: "TeamLineups",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CaptainPlayerId",
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

            migrationBuilder.AddColumn<int>(
                name: "LongFreeKickPlayerId",
                table: "TeamLineups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PenaltyKickPlayerId",
                table: "TeamLineups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeftCornerPlayerId",
                table: "TeamLineups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RightCornerPlayerId",
                table: "TeamLineups",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"TeamLineups\" SET \"Name\" = 'Escalação' WHERE \"Name\" IS NULL OR TRIM(\"Name\") = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "TeamLineups");

            migrationBuilder.DropColumn(
                name: "Observation",
                table: "TeamLineups");

            migrationBuilder.DropColumn(
                name: "CaptainPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropColumn(
                name: "ShortFreeKickLeftPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropColumn(
                name: "ShortFreeKickRightPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropColumn(
                name: "LongFreeKickPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropColumn(
                name: "PenaltyKickPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropColumn(
                name: "LeftCornerPlayerId",
                table: "TeamLineups");

            migrationBuilder.DropColumn(
                name: "RightCornerPlayerId",
                table: "TeamLineups");
        }
    }
}
