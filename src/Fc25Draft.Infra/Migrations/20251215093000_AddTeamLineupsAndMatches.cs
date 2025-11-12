using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamLineupsAndMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwayTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    KickoffAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    HomeLineupSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    AwayLineupSnapshotJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.MatchId);
                    table.ForeignKey(
                        name: "FK_Matches_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamLineups",
                columns: table => new
                {
                    LineupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormationCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TacticCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamLineups", x => x.LineupId);
                    table.ForeignKey(
                        name: "FK_TeamLineups_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamLineupSlots",
                columns: table => new
                {
                    SlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<byte>(type: "smallint", nullable: false),
                    PrimaryPositionId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamLineupSlots", x => x.SlotId);
                    table.ForeignKey(
                        name: "FK_TeamLineupSlots_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TeamLineupSlots_TeamLineups_LineupId",
                        column: x => x.LineupId,
                        principalTable: "TeamLineups",
                        principalColumn: "LineupId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_AwayTeamId",
                table: "Matches",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_HomeTeamId",
                table: "Matches",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_HomeTeamId_AwayTeamId_KickoffAtUtc",
                table: "Matches",
                columns: new[] { "HomeTeamId", "AwayTeamId", "KickoffAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineups_TeamId",
                table: "TeamLineups",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineupSlots_LineupId_Role_Order",
                table: "TeamLineupSlots",
                columns: new[] { "LineupId", "Role", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineupSlots_PlayerId",
                table: "TeamLineupSlots",
                column: "PlayerId");

            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("CREATE UNIQUE INDEX \"UX_TeamLineups_Team_Active\" ON \"TeamLineups\" (\"TeamId\") WHERE \"IsActive\" = TRUE;");
            }
            else
            {
                migrationBuilder.Sql("CREATE UNIQUE INDEX UX_TeamLineups_Team_Active ON TeamLineups(TeamId) WHERE IsActive = 1;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("DROP INDEX IF EXISTS \"UX_TeamLineups_Team_Active\";");
            }
            else
            {
                migrationBuilder.Sql(@"IF EXISTS (SELECT name FROM sys.indexes WHERE name = 'UX_TeamLineups_Team_Active')
BEGIN
    DROP INDEX UX_TeamLineups_Team_Active ON TeamLineups;
END");
            }

            migrationBuilder.DropTable(
                name: "TeamLineupSlots");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "TeamLineups");
        }
    }
}
