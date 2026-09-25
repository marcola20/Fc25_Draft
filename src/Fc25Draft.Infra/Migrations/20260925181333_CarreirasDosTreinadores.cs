using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <summary>
    /// A carreira de cada um como ela foi de verdade. O backfill anterior chutou a data de
    /// entrada pela primeira liga do clube e não sabia das trocas de comando no meio do
    /// caminho; aqui as passagens são refeitas a partir do histórico da liga.
    /// </summary>
    public partial class CarreirasDosTreinadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- Quem passou pela liga mas não tinha cadastro (saiu antes do sistema existir).
                INSERT INTO ""Treinadores"" (""TreinadorId"", ""Nome"", ""Token"", ""Ativo"", ""CriadoEm"")
                SELECT gen_random_uuid(), v.nome,
                       upper(regexp_replace(v.nome, '[^A-Za-z0-9]', '', 'g')) || '-' || upper(substr(replace(gen_random_uuid()::text, '-', ''), 1, 6)),
                       v.ativo, now() AT TIME ZONE 'utc'
                FROM (VALUES
                    ('Marcola',     true),
                    ('Gabriel Pio', false)
                ) AS v(nome, ativo)
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""Treinadores"" t WHERE lower(btrim(t.""Nome"")) = lower(v.nome));

                -- As passagens são reescritas inteiras: a lista abaixo é o histórico oficial.
                DELETE FROM ""TreinadorPassagens"";

                INSERT INTO ""TreinadorPassagens"" (""PassagemId"", ""TreinadorId"", ""TimeId"", ""Papel"", ""Desde"", ""Ate"")
                SELECT gen_random_uuid(), t.""TreinadorId"", tm.""TeamId"", v.papel, v.desde, v.ate
                FROM (VALUES
                    -- Quem estava lá desde a primeira temporada e segue no mesmo clube.
                    ('Kaio',           'Fluminense',  1, DATE '2026-04-08', NULL::date),
                    ('Renan',          'Internacional', 1, DATE '2026-04-08', NULL::date),
                    ('Rafa',           'Santos',      1, DATE '2026-04-08', NULL::date),
                    ('Guilherme',      'Cruzeiro',    1, DATE '2026-04-08', NULL::date),
                    ('Gui Gomes',      'São Paulo',   1, DATE '2026-04-08', NULL::date),
                    ('Mafra',          'Botafogo',    1, DATE '2026-04-08', NULL::date),
                    ('Koichi',         'Palmeiras',   2, DATE '2026-04-08', NULL::date),

                    -- Flamengo: Albert saiu na quarta rodada da segunda temporada.
                    ('Albert',         'Flamengo',    1, DATE '2026-04-08', DATE '2026-08-27'),
                    ('Victor Formoso', 'Flamengo',    1, DATE '2026-08-27', NULL::date),
                    ('Albert',         'Fluminense',  2, DATE '2026-09-21', NULL::date),

                    -- Grêmio: Marcola na primeira temporada, Teuzin da segunda em diante.
                    ('Marcola',        'Grêmio',      1, DATE '2026-04-08', DATE '2026-05-22'),
                    ('Teuzin',         'Grêmio',      1, DATE '2026-05-22', NULL::date),

                    -- Vasco: Gabriel Pio até o fim da segunda temporada, Portuga assume.
                    ('Gabriel Pio',    'Vasco',       1, DATE '2026-04-08', DATE '2026-09-21'),
                    ('Portuga',        'Vasco',       2, DATE '2026-08-27', DATE '2026-09-21'),
                    ('Portuga',        'Vasco',       1, DATE '2026-09-21', NULL::date),

                    -- A dança das cadeiras do fim da segunda temporada.
                    ('João Vilas',     'Coritiba',    1, DATE '2026-04-08', DATE '2026-09-16'),
                    ('Marcola',        'Coritiba',    1, DATE '2026-09-17', DATE '2026-09-21'),
                    ('João Vilas',     'Corinthians', 2, DATE '2026-09-18', DATE '2026-09-21'),
                    ('João Vilas',     'Palmeiras',   1, DATE '2026-09-21', NULL::date),
                    ('L. Felipe',      'Palmeiras',   1, DATE '2026-04-08', DATE '2026-09-21'),
                    ('L. Felipe',      'Corinthians', 1, DATE '2026-09-21', NULL::date),
                    ('JG',             'Corinthians', 1, DATE '2026-04-08', DATE '2026-09-21'),
                    ('JG',             'Coritiba',    1, DATE '2026-09-21', NULL::date),

                    -- Os times da expansão, todos a partir do draft.
                    ('Paulo',          'Juventude',   1, DATE '2026-09-17', NULL::date),
                    ('Laporte',        'Figueirense', 1, DATE '2026-09-17', NULL::date),
                    ('Henrique',       'Vitória',     1, DATE '2026-09-17', NULL::date),
                    ('Machado',        'Vitória',     2, DATE '2026-09-17', NULL::date),
                    ('João Pedro',     'Náutico',     1, DATE '2026-09-17', NULL::date),
                    ('Pedro Boka',     'Sport',       1, DATE '2026-09-17', NULL::date),
                    ('Xina',           'Paraná',      1, DATE '2026-09-17', NULL::date)
                ) AS v(nome, time, papel, desde, ate)
                JOIN ""Treinadores"" t ON lower(btrim(t.""Nome"")) = lower(v.nome)
                JOIN ""Teams"" tm ON lower(btrim(tm.""TeamName"")) = lower(v.time);

                -- O cadastro do time acompanha quem está no comando hoje.
                UPDATE ""Teams"" tm SET ""OwnerName"" = (
                    SELECT t.""Nome"" FROM ""TreinadorPassagens"" p
                    JOIN ""Treinadores"" t ON t.""TreinadorId"" = p.""TreinadorId""
                    WHERE p.""TimeId"" = tm.""TeamId"" AND p.""Ate"" IS NULL AND p.""Papel"" = 1
                    LIMIT 1)
                WHERE EXISTS (
                    SELECT 1 FROM ""TreinadorPassagens"" p
                    WHERE p.""TimeId"" = tm.""TeamId"" AND p.""Ate"" IS NULL AND p.""Papel"" = 1);

                UPDATE ""Teams"" tm SET ""AuxiliarName"" = (
                    SELECT t.""Nome"" FROM ""TreinadorPassagens"" p
                    JOIN ""Treinadores"" t ON t.""TreinadorId"" = p.""TreinadorId""
                    WHERE p.""TimeId"" = tm.""TeamId"" AND p.""Ate"" IS NULL AND p.""Papel"" = 2
                    LIMIT 1);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O histórico anterior era um chute do backfill; não há para onde voltar.
        }
    }
}
