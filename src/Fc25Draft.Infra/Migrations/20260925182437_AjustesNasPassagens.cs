using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <summary>
    /// Movimentações avulsas de setembro: o Koichi deixou o Palmeiras na troca de comando
    /// e o Lúcio chegou como auxiliar do Santos.
    /// </summary>
    public partial class AjustesNasPassagens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- Koichi saiu do Palmeiras no dia em que o João Vilas assumiu.
                UPDATE ""TreinadorPassagens"" p
                   SET ""Ate"" = DATE '2026-09-21'
                  FROM ""Treinadores"" t, ""Teams"" tm
                 WHERE t.""TreinadorId"" = p.""TreinadorId""
                   AND tm.""TeamId"" = p.""TimeId""
                   AND lower(btrim(t.""Nome"")) = 'koichi'
                   AND lower(btrim(tm.""TeamName"")) = 'palmeiras'
                   AND p.""Papel"" = 2
                   AND p.""Ate"" IS NULL;

                -- Lúcio, auxiliar do Santos desde 25/09.
                INSERT INTO ""Treinadores"" (""TreinadorId"", ""Nome"", ""Token"", ""Ativo"", ""CriadoEm"")
                SELECT gen_random_uuid(), 'Lucio',
                       'LUCIO-' || upper(substr(replace(gen_random_uuid()::text, '-', ''), 1, 6)),
                       true, now() AT TIME ZONE 'utc'
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""Treinadores"" t WHERE lower(btrim(t.""Nome"")) = 'lucio');

                INSERT INTO ""TreinadorPassagens"" (""PassagemId"", ""TreinadorId"", ""TimeId"", ""Papel"", ""Desde"", ""Ate"")
                SELECT gen_random_uuid(), t.""TreinadorId"", tm.""TeamId"", 2, DATE '2026-09-25', NULL
                FROM ""Treinadores"" t, ""Teams"" tm
                WHERE lower(btrim(t.""Nome"")) = 'lucio'
                  AND lower(btrim(tm.""TeamName"")) = 'santos'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""TreinadorPassagens"" p
                      WHERE p.""TreinadorId"" = t.""TreinadorId"" AND p.""TimeId"" = tm.""TeamId"");

                -- O cadastro dos dois clubes acompanha.
                UPDATE ""Teams"" tm SET ""AuxiliarName"" = (
                    SELECT t.""Nome"" FROM ""TreinadorPassagens"" p
                    JOIN ""Treinadores"" t ON t.""TreinadorId"" = p.""TreinadorId""
                    WHERE p.""TimeId"" = tm.""TeamId"" AND p.""Ate"" IS NULL AND p.""Papel"" = 2
                    LIMIT 1)
                WHERE lower(btrim(tm.""TeamName"")) IN ('palmeiras', 'santos');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Movimentação de elenco não volta atrás.
        }
    }
}
