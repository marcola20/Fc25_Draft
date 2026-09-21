using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class SeedPremiacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Premiações que a liga já usou, para a página nascer com histórico:
            // 2008/2009 com 12 times (a tabela antiga) e 2010 com Série A, Série B, Copa e Supercopa.
            migrationBuilder.Sql(@"
INSERT INTO ""Premiacoes"" (""PremiacaoId"", ""Temporada"", ""Nome"", ""CriadoEm"", ""AtualizadoEm"")
SELECT gen_random_uuid(), t.temporada, t.nome, now() AT TIME ZONE 'utc', now() AT TIME ZONE 'utc'
FROM (VALUES
    (2008, 'Premiação 2008 (1ª temporada)'),
    (2009, 'Premiação 2009 (2ª temporada)'),
    (2010, 'Premiação 2010 (3ª temporada)')
) AS t(temporada, nome)
WHERE NOT EXISTS (SELECT 1 FROM ""Premiacoes"" p WHERE p.""Temporada"" = t.temporada);

INSERT INTO ""PremiacaoItens"" (""PremiacaoItemId"", ""PremiacaoId"", ""Tipo"", ""Divisao"", ""PosicaoDe"", ""PosicaoAte"", ""Fase"", ""Valor"")
SELECT gen_random_uuid(), p.""PremiacaoId"", v.tipo, v.divisao, v.posicao, v.posicao, v.fase, v.valor
FROM ""Premiacoes"" p
JOIN (VALUES
    -- Temporadas 2008 e 2009: Série A com 12 times, quanto pior a posição maior o prêmio.
    (2008, 0, 1::int,  1::int,  NULL::int, 2000000::numeric),
    (2008, 0, 1,  2,  NULL,  4000000),
    (2008, 0, 1,  3,  NULL,  6000000),
    (2008, 0, 1,  4,  NULL,  9000000),
    (2008, 0, 1,  5,  NULL, 12000000),
    (2008, 0, 1,  6,  NULL, 16000000),
    (2008, 0, 1,  7,  NULL, 21000000),
    (2008, 0, 1,  8,  NULL, 26000000),
    (2008, 0, 1,  9,  NULL, 32000000),
    (2008, 0, 1, 10,  NULL, 38000000),
    (2008, 0, 1, 11,  NULL, 44000000),
    (2008, 0, 1, 12,  NULL, 50000000),
    (2009, 0, 1,  1,  NULL,  2000000),
    (2009, 0, 1,  2,  NULL,  4000000),
    (2009, 0, 1,  3,  NULL,  6000000),
    (2009, 0, 1,  4,  NULL,  9000000),
    (2009, 0, 1,  5,  NULL, 12000000),
    (2009, 0, 1,  6,  NULL, 16000000),
    (2009, 0, 1,  7,  NULL, 21000000),
    (2009, 0, 1,  8,  NULL, 26000000),
    (2009, 0, 1,  9,  NULL, 32000000),
    (2009, 0, 1, 10,  NULL, 38000000),
    (2009, 0, 1, 11,  NULL, 44000000),
    (2009, 0, 1, 12,  NULL, 50000000),
    -- Copa 2009: dois grupos, sem quartas.
    (2009, 1, NULL, NULL, 1, 35000000),
    (2009, 1, NULL, NULL, 2, 20000000),
    (2009, 1, NULL, NULL, 3, 10000000),
    (2009, 1, NULL, NULL, 5,  4000000),
    -- Temporada 2010: Série A com 10 times.
    (2010, 0, 1,  1,  NULL, 15000000),
    (2010, 0, 1,  2,  NULL, 15000000),
    (2010, 0, 1,  3,  NULL, 15000000),
    (2010, 0, 1,  4,  NULL, 15000000),
    (2010, 0, 1,  5,  NULL, 20000000),
    (2010, 0, 1,  6,  NULL, 24000000),
    (2010, 0, 1,  7,  NULL, 29000000),
    (2010, 0, 1,  8,  NULL, 34000000),
    (2010, 0, 1,  9,  NULL, 40000000),
    (2010, 0, 1, 10,  NULL, 40000000),
    -- Série B: mesmo valor para todos, inclusive quem sobe.
    (2010, 0, 2,  1,  NULL, 40000000),
    (2010, 0, 2,  2,  NULL, 40000000),
    (2010, 0, 2,  3,  NULL, 40000000),
    (2010, 0, 2,  4,  NULL, 40000000),
    (2010, 0, 2,  5,  NULL, 40000000),
    (2010, 0, 2,  6,  NULL, 40000000),
    (2010, 0, 2,  7,  NULL, 40000000),
    (2010, 0, 2,  8,  NULL, 40000000),
    -- Copa 2010: quatro grupos, com quartas.
    (2010, 1, NULL, NULL, 1, 35000000),
    (2010, 1, NULL, NULL, 2, 20000000),
    (2010, 1, NULL, NULL, 3, 10000000),
    (2010, 1, NULL, NULL, 4,  6000000),
    (2010, 1, NULL, NULL, 5,  4000000),
    -- Supercopa: só o campeão.
    (2010, 2, NULL, NULL, 1,  5000000)
) AS v(temporada, tipo, divisao, posicao, fase, valor) ON v.temporada = p.""Temporada""
WHERE NOT EXISTS (SELECT 1 FROM ""PremiacaoItens"" i WHERE i.""PremiacaoId"" = p.""PremiacaoId"");
");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
