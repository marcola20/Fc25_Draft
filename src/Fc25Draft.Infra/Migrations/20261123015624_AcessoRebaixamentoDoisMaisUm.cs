using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <summary>
    /// Proposta aprovada para 2011: na Série A o 9º e o 10º caem direto e o 8º joga o playoff;
    /// na Série B o 1º e o 2º sobem direto e o 3º joga o playoff. As temporadas anteriores ficam como foram.
    /// </summary>
    public partial class AcessoRebaixamentoDoisMaisUm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tipo 0 = Liga; Divisao 1 = Série A, 2 = Série B.
            migrationBuilder.Sql("""
                UPDATE "Ligas"
                   SET "VagasDiretas" = 2, "VagasPlayoff" = 1
                 WHERE "Tipo" = 0 AND "Divisao" IN (1, 2) AND "Temporada" >= 2011;
                """);

            // Regulamento de 2011 em diante criado copiando o de 2010: troca as duas linhas de acesso/rebaixamento.
            // Sem essas linhas (ainda não criado ou já editado à mão), não muda nada.
            migrationBuilder.Sql("""
                UPDATE "Regulamentos"
                   SET "Conteudo" = replace(replace("Conteudo",
                         '<strong>Nesta temporada de transição, caem 2 times</strong> para a Série B e não há playoff',
                         '<strong>Caem direto</strong> o <strong>9° e o 10° da Série A</strong>; <strong>sobem direto</strong> o <strong>1° e o 2° da Série B</strong>'),
                         'Nas próximas temporadas: o <strong>último da Série A cai</strong>, o <strong>campeão da Série B sobe</strong> e o <strong>9° da A enfrenta o 2° da B</strong> em jogo único pela última vaga',
                         'O <strong>8° da Série A enfrenta o 3° da Série B</strong> em jogo único pela última vaga (empate vai para os pênaltis)'),
                       "AtualizadoEm" = now() at time zone 'utc'
                 WHERE "Temporada" >= 2011
                   AND "Conteudo" LIKE '%9° da A enfrenta o 2° da B%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Ligas"
                   SET "VagasDiretas" = 1, "VagasPlayoff" = 1
                 WHERE "Tipo" = 0 AND "Divisao" IN (1, 2) AND "Temporada" >= 2011;
                """);
        }
    }
}
