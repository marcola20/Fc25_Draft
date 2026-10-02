using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class EscalacaoSemQuemSaiuDoElenco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Daqui em diante quem sai do elenco sai da escalação no mesmo save; aqui limpa o que ficou de antes:
            // vagas (titular e reserva), capitão e cobranças com jogador que não é mais do time.
            migrationBuilder.Sql(@"
                UPDATE ""TeamLineupSlots"" s SET ""PlayerId"" = NULL
                FROM ""TeamLineups"" l
                WHERE s.""LineupId"" = l.""LineupId""
                  AND s.""PlayerId"" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM ""TeamRosters"" r WHERE r.""TeamId"" = l.""TeamId"" AND r.""PlayerId"" = s.""PlayerId"");");
            migrationBuilder.Sql(@"
                UPDATE ""TeamLineups"" l SET ""CaptainPlayerId"" = NULL
                WHERE l.""CaptainPlayerId"" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM ""TeamRosters"" r WHERE r.""TeamId"" = l.""TeamId"" AND r.""PlayerId"" = l.""CaptainPlayerId"");");
            migrationBuilder.Sql(@"
                UPDATE ""TeamLineups"" l SET ""ShortFreeKick1PlayerId"" = NULL
                WHERE l.""ShortFreeKick1PlayerId"" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM ""TeamRosters"" r WHERE r.""TeamId"" = l.""TeamId"" AND r.""PlayerId"" = l.""ShortFreeKick1PlayerId"");");
            migrationBuilder.Sql(@"
                UPDATE ""TeamLineups"" l SET ""ShortFreeKick2PlayerId"" = NULL
                WHERE l.""ShortFreeKick2PlayerId"" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM ""TeamRosters"" r WHERE r.""TeamId"" = l.""TeamId"" AND r.""PlayerId"" = l.""ShortFreeKick2PlayerId"");");
            migrationBuilder.Sql(@"
                UPDATE ""TeamLineups"" l SET ""LongFreeKickPlayerId"" = NULL
                WHERE l.""LongFreeKickPlayerId"" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM ""TeamRosters"" r WHERE r.""TeamId"" = l.""TeamId"" AND r.""PlayerId"" = l.""LongFreeKickPlayerId"");");
            migrationBuilder.Sql(@"
                UPDATE ""TeamLineups"" l SET ""PenaltiesPlayerId"" = NULL
                WHERE l.""PenaltiesPlayerId"" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM ""TeamRosters"" r WHERE r.""TeamId"" = l.""TeamId"" AND r.""PlayerId"" = l.""PenaltiesPlayerId"");");
            migrationBuilder.Sql(@"
                UPDATE ""TeamLineups"" l SET ""CornerLeftPlayerId"" = NULL
                WHERE l.""CornerLeftPlayerId"" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM ""TeamRosters"" r WHERE r.""TeamId"" = l.""TeamId"" AND r.""PlayerId"" = l.""CornerLeftPlayerId"");");
            migrationBuilder.Sql(@"
                UPDATE ""TeamLineups"" l SET ""CornerRightPlayerId"" = NULL
                WHERE l.""CornerRightPlayerId"" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM ""TeamRosters"" r WHERE r.""TeamId"" = l.""TeamId"" AND r.""PlayerId"" = l.""CornerRightPlayerId"");");
            migrationBuilder.Sql(@"
                UPDATE ""TeamLineups"" l SET ""AttackingPlayer1Id"" = NULL
                WHERE l.""AttackingPlayer1Id"" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM ""TeamRosters"" r WHERE r.""TeamId"" = l.""TeamId"" AND r.""PlayerId"" = l.""AttackingPlayer1Id"");");
            migrationBuilder.Sql(@"
                UPDATE ""TeamLineups"" l SET ""AttackingPlayer2Id"" = NULL
                WHERE l.""AttackingPlayer2Id"" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM ""TeamRosters"" r WHERE r.""TeamId"" = l.""TeamId"" AND r.""PlayerId"" = l.""AttackingPlayer2Id"");");
            migrationBuilder.Sql(@"
                UPDATE ""TeamLineups"" l SET ""AttackingPlayer3Id"" = NULL
                WHERE l.""AttackingPlayer3Id"" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM ""TeamRosters"" r WHERE r.""TeamId"" = l.""TeamId"" AND r.""PlayerId"" = l.""AttackingPlayer3Id"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não dá para devolver quem estava escalado; nada a desfazer.
        }
    }
}
