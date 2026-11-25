using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <summary>
    /// O Gerar Automático não repete mais os jogos da temporada anterior (o calendário igual era a ordem
    /// das rodadas, não os confrontos). Tira o parágrafo do "segundo turno" do anúncio do calendário 2011.
    /// </summary>
    public partial class AnuncioCalendarioSemSegundoTurno : Migration
    {
        private const string IdCalendario = "7a3c9d2f-4e5b-4c6d-8f20-1b2c3d4e5f11";

        private const string TrechoTexto =
            "\n\n*Segundo turno*: os jogos seguem a mesma ordem da temporada passada, com o mando invertido. Quem subiu entra no lugar de quem caiu.";

        private const string TrechoExplicacao =
            "\n\nSegundo turno: o Gerar Automático da Série A e da B repete, rodada por rodada, os jogos da mesma divisão em 2010, com o mando invertido. Quem subiu fica no lugar de quem fez o caminho contrário: na A, o campeão da B no lugar do último da A e o vencedor do playoff no lugar de quem perdeu o playoff; na B, o contrário. A aba Rodadas mostra quem entra no lugar de quem antes de gerar.";

        private const string TrechoSite =
            "\n\n## Segundo turno\nOs jogos seguem a *mesma ordem da temporada passada*, com o *mando invertido*. Quem subiu entra no lugar de quem caiu.\n\n> Exemplo: se na 1ª rodada de 2010 foi Botafogo x Palmeiras e o Palmeiras desceu, em 2011 é Corinthians x Botafogo, com o Corinthians (que subiu) no lugar dele e o mando trocado.";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE "Anuncios" SET
                    "Texto" = REPLACE("Texto", $t${TrechoTexto}$t$, ''),
                    "Explicacao" = REPLACE("Explicacao", $t${TrechoExplicacao}$t$, ''),
                    "TextoSite" = REPLACE("TextoSite", $t${TrechoSite}$t$, '')
                WHERE "AnuncioId" = '{IdCalendario}';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
