using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <summary>
    /// O pick do draft inseria o jogador no elenco sem preencher Players.CurrentTeamId.
    /// Limite de elenco (23) e gerador de mercado usam CurrentTeamId, então alinha com TeamRosters.
    /// </summary>
    public partial class SyncPlayerCurrentTeamWithRoster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Players" p
                SET "CurrentTeamId" = r."TeamId"
                FROM "TeamRosters" r
                WHERE r."PlayerId" = p."PlayerId"
                  AND p."CurrentTeamId" IS DISTINCT FROM r."TeamId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Correção de dados: não há estado anterior para restaurar.
        }
    }
}
