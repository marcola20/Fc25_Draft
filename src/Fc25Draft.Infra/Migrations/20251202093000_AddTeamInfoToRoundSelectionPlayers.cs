using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamInfoToRoundSelectionPlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "RoundSelectionPlayers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamName",
                table: "RoundSelectionPlayers",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""RoundSelectionPlayers"" rsp
                SET ""TeamId"" = p.""CurrentTeamId"",
                    ""TeamName"" = t.""TeamName""
                FROM ""Players"" p
                LEFT JOIN ""Teams"" t ON t.""TeamId"" = p.""CurrentTeamId""
                WHERE rsp.""PlayerGuid"" = p.""PlayerGuid""
                    AND (rsp.""TeamId"" IS NULL OR rsp.""TeamName"" IS NULL);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "RoundSelectionPlayers");

            migrationBuilder.DropColumn(
                name: "TeamName",
                table: "RoundSelectionPlayers");
        }
    }
}
