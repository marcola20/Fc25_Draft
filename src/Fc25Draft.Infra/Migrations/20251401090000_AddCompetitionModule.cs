using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Competitions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Competitions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Competitions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Rounds",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<int>(
                name: "RoundNumber",
                table: "Rounds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledAtUtc",
                table: "Rounds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Rounds",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.CreateTable(
                name: "CompetitionTeams",
                columns: table => new
                {
                    CompetitionTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    InitialBudget = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionTeams", x => x.CompetitionTeamId);
                    table.ForeignKey(
                        name: "FK_CompetitionTeams_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "CompetitionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionTeams_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionMatches",
                columns: table => new
                {
                    CompetitionMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeCompetitionTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwayCompetitionTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HomeGoals = table.Column<int>(type: "integer", nullable: true),
                    AwayGoals = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Stadium = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Observations = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionMatches", x => x.CompetitionMatchId);
                    table.ForeignKey(
                        name: "FK_CompetitionMatches_CompetitionTeams_AwayCompetitionTeamId",
                        column: x => x.AwayCompetitionTeamId,
                        principalTable: "CompetitionTeams",
                        principalColumn: "CompetitionTeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionMatches_CompetitionTeams_HomeCompetitionTeamId",
                        column: x => x.HomeCompetitionTeamId,
                        principalTable: "CompetitionTeams",
                        principalColumn: "CompetitionTeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionMatches_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "CompetitionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionMatches_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "RoundId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionLogs",
                columns: table => new
                {
                    CompetitionLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompetitionMatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PerformedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Details = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionLogs", x => x.CompetitionLogId);
                    table.ForeignKey(
                        name: "FK_CompetitionLogs_CompetitionMatches_CompetitionMatchId",
                        column: x => x.CompetitionMatchId,
                        principalTable: "CompetitionMatches",
                        principalColumn: "CompetitionMatchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionLogs_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "CompetitionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionPlayerStats",
                columns: table => new
                {
                    CompetitionPlayerStatId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    MatchesPlayed = table.Column<int>(type: "integer", nullable: false),
                    Goals = table.Column<int>(type: "integer", nullable: false),
                    Assists = table.Column<int>(type: "integer", nullable: false),
                    YellowCards = table.Column<int>(type: "integer", nullable: false),
                    RedCards = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionPlayerStats", x => x.CompetitionPlayerStatId);
                    table.ForeignKey(
                        name: "FK_CompetitionPlayerStats_CompetitionTeams_CompetitionTeamId",
                        column: x => x.CompetitionTeamId,
                        principalTable: "CompetitionTeams",
                        principalColumn: "CompetitionTeamId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionPlayerStats_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "CompetitionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionPlayerStats_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionTeamStats",
                columns: table => new
                {
                    CompetitionTeamStatId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchesPlayed = table.Column<int>(type: "integer", nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: false),
                    Draws = table.Column<int>(type: "integer", nullable: false),
                    Losses = table.Column<int>(type: "integer", nullable: false),
                    GoalsFor = table.Column<int>(type: "integer", nullable: false),
                    GoalsAgainst = table.Column<int>(type: "integer", nullable: false),
                    GoalDifference = table.Column<int>(type: "integer", nullable: false),
                    YellowCards = table.Column<int>(type: "integer", nullable: false),
                    RedCards = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionTeamStats", x => x.CompetitionTeamStatId);
                    table.ForeignKey(
                        name: "FK_CompetitionTeamStats_CompetitionTeams_CompetitionTeamId",
                        column: x => x.CompetitionTeamId,
                        principalTable: "CompetitionTeams",
                        principalColumn: "CompetitionTeamId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionTeamStats_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "CompetitionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionMatchEvents",
                columns: table => new
                {
                    CompetitionMatchEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: true),
                    RelatedPlayerId = table.Column<int>(type: "integer", nullable: true),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Minute = table.Column<int>(type: "integer", nullable: true),
                    Observations = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionMatchEvents", x => x.CompetitionMatchEventId);
                    table.ForeignKey(
                        name: "FK_CompetitionMatchEvents_CompetitionMatches_CompetitionMatchId",
                        column: x => x.CompetitionMatchId,
                        principalTable: "CompetitionMatches",
                        principalColumn: "CompetitionMatchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionMatchEvents_CompetitionTeams_CompetitionTeamId",
                        column: x => x.CompetitionTeamId,
                        principalTable: "CompetitionTeams",
                        principalColumn: "CompetitionTeamId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionMatchEvents_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionMatchEvents_Players_RelatedPlayerId",
                        column: x => x.RelatedPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionStandings",
                columns: table => new
                {
                    CompetitionStandingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    MatchesPlayed = table.Column<int>(type: "integer", nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: false),
                    Draws = table.Column<int>(type: "integer", nullable: false),
                    Losses = table.Column<int>(type: "integer", nullable: false),
                    GoalsFor = table.Column<int>(type: "integer", nullable: false),
                    GoalsAgainst = table.Column<int>(type: "integer", nullable: false),
                    GoalDifference = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    YellowCards = table.Column<int>(type: "integer", nullable: false),
                    RedCards = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionStandings", x => x.CompetitionStandingId);
                    table.ForeignKey(
                        name: "FK_CompetitionStandings_CompetitionTeams_CompetitionTeamId",
                        column: x => x.CompetitionTeamId,
                        principalTable: "CompetitionTeams",
                        principalColumn: "CompetitionTeamId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionStandings_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "CompetitionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionLogs_CompetitionId",
                table: "CompetitionLogs",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionLogs_CompetitionMatchId",
                table: "CompetitionLogs",
                column: "CompetitionMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionMatchEvents_CompetitionMatchId",
                table: "CompetitionMatchEvents",
                column: "CompetitionMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionMatchEvents_CompetitionTeamId",
                table: "CompetitionMatchEvents",
                column: "CompetitionTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionMatchEvents_PlayerId",
                table: "CompetitionMatchEvents",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionMatchEvents_RelatedPlayerId",
                table: "CompetitionMatchEvents",
                column: "RelatedPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionMatches_AwayCompetitionTeamId",
                table: "CompetitionMatches",
                column: "AwayCompetitionTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionMatches_CompetitionId",
                table: "CompetitionMatches",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionMatches_HomeCompetitionTeamId",
                table: "CompetitionMatches",
                column: "HomeCompetitionTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionMatches_RoundId_HomeCompetitionTeamId_AwayCompetitionTeamId",
                table: "CompetitionMatches",
                columns: new[] { "RoundId", "HomeCompetitionTeamId", "AwayCompetitionTeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPlayerStats_CompetitionId_PlayerId_CompetitionTeamId",
                table: "CompetitionPlayerStats",
                columns: new[] { "CompetitionId", "PlayerId", "CompetitionTeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPlayerStats_CompetitionTeamId",
                table: "CompetitionPlayerStats",
                column: "CompetitionTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPlayerStats_PlayerId",
                table: "CompetitionPlayerStats",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionStandings_CompetitionId",
                table: "CompetitionStandings",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionStandings_CompetitionId_Position",
                table: "CompetitionStandings",
                columns: new[] { "CompetitionId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionStandings_CompetitionId_CompetitionTeamId",
                table: "CompetitionStandings",
                columns: new[] { "CompetitionId", "CompetitionTeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionTeamStats_CompetitionId",
                table: "CompetitionTeamStats",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionTeamStats_CompetitionId_CompetitionTeamId",
                table: "CompetitionTeamStats",
                columns: new[] { "CompetitionId", "CompetitionTeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionTeamStats_CompetitionTeamId",
                table: "CompetitionTeamStats",
                column: "CompetitionTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionTeams_CompetitionId_TeamId",
                table: "CompetitionTeams",
                columns: new[] { "CompetitionId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionTeams_TeamId",
                table: "CompetitionTeams",
                column: "TeamId");

            migrationBuilder.Sql(@"UPDATE ""Competitions"" SET ""CreatedAtUtc"" = NOW() WHERE ""CreatedAtUtc"" = '1970-01-01 00:00:00+00';");
            migrationBuilder.Sql(@"UPDATE ""Competitions"" SET ""UpdatedAtUtc"" = NOW() WHERE ""UpdatedAtUtc"" = '1970-01-01 00:00:00+00';");
            migrationBuilder.Sql(@"UPDATE ""Rounds"" SET ""CreatedAtUtc"" = NOW() WHERE ""CreatedAtUtc"" = '1970-01-01 00:00:00+00';");
            migrationBuilder.Sql(@"UPDATE ""Rounds"" SET ""UpdatedAtUtc"" = NOW() WHERE ""UpdatedAtUtc"" = '1970-01-01 00:00:00+00';");
            migrationBuilder.Sql(@"WITH ranked AS (SELECT ""RoundId", ROW_NUMBER() OVER(PARTITION BY ""CompetitionId"" ORDER BY ""RoundId"") AS rn FROM ""Rounds"") UPDATE ""Rounds"" r SET ""RoundNumber"" = ranked.rn FROM ranked WHERE ranked.""RoundId"" = r.""RoundId"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionLogs");

            migrationBuilder.DropTable(
                name: "CompetitionMatchEvents");

            migrationBuilder.DropTable(
                name: "CompetitionPlayerStats");

            migrationBuilder.DropTable(
                name: "CompetitionStandings");

            migrationBuilder.DropTable(
                name: "CompetitionTeamStats");

            migrationBuilder.DropTable(
                name: "CompetitionMatches");

            migrationBuilder.DropTable(
                name: "CompetitionTeams");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Competitions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Competitions");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Competitions");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Rounds");

            migrationBuilder.DropColumn(
                name: "RoundNumber",
                table: "Rounds");

            migrationBuilder.DropColumn(
                name: "ScheduledAtUtc",
                table: "Rounds");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Rounds");
        }
    }
}
