using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <summary>
    /// Anúncios do calendário de 2011 e da Copa em fase de liga. Entram como rascunho: nem enviados no
    /// grupo nem publicados no site; quem decide é o admin em /admin/anuncios.
    /// </summary>
    public partial class AnunciosCalendarioECopa2011 : Migration
    {
        private const string IdCalendario = "7a3c9d2f-4e5b-4c6d-8f20-1b2c3d4e5f11";
        private const string IdCopa = "7a3c9d2f-4e5b-4c6d-8f20-1b2c3d4e5f12";
        private const string Grupo = "Virada para 2011";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var criadoEm = new DateTime(2026, 11, 23, 19, 0, 0);
            var colunas = new[]
            {
                "AnuncioId", "Grupo", "Titulo", "Texto", "Explicacao", "Resumo", "TextoSite",
                "Ordem", "EnviadoEm", "PublicadoEm", "CriadoEm", "AtualizadoEm"
            };

            migrationBuilder.InsertData(
                table: "Anuncios",
                columns: colunas,
                values: new object[]
                {
                    Guid.Parse(IdCalendario), Grupo, "Calendário da temporada 2011",
                    Normalizar(TextoCalendario), Normalizar(ExplicacaoCalendario), ResumoCalendario, Normalizar(SiteCalendario),
                    5, null, null, criadoEm, criadoEm
                });

            migrationBuilder.InsertData(
                table: "Anuncios",
                columns: colunas,
                values: new object[]
                {
                    Guid.Parse(IdCopa), Grupo, "Copa em fase de liga",
                    Normalizar(TextoCopa), Normalizar(ExplicacaoCopa), ResumoCopa, Normalizar(SiteCopa),
                    6, null, null, criadoEm, criadoEm
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "Anuncios", keyColumn: "AnuncioId", keyValue: Guid.Parse(IdCalendario));
            migrationBuilder.DeleteData(table: "Anuncios", keyColumn: "AnuncioId", keyValue: Guid.Parse(IdCopa));
        }

        private static string Normalizar(string texto) => texto.Replace("\r\n", "\n");

        // ── Calendário ──────────────────────────────────────────────────────────────────────

        private const string TextoCalendario =
            """
            *Calendário da temporada 2011* 📅

            *Quarta 25/11*: fecha a janela de transferências.
            *Quinta 26/11*: Supercopa (campeão da Série A x campeão da Copa) abre a temporada.
            *Sábado 28/11*: 1ª rodada da Série A.

            A partir da semana seguinte o ritmo é fixo:
            - *Terça*: Série B
            - *Quinta*: Série A
            - *Sábado*: Copa

            A Série B termina em 12/01 e a Série A em 21/01 (ela tem mais rodadas). A final da Copa é no sábado 23/01 e o playoff de acesso fecha a temporada no sábado 30/01.

            O calendário completo está na página *Formato* do site.
            """;

        private const string ExplicacaoCalendario =
            """
            O calendário sai da abertura gravada em Admin › Temporada (card Calendário): a abertura é o dia da Supercopa (26/11/2026). A janela fecha na véspera, a Série A estreia no sábado seguinte e, da semana depois em diante, é terça Série B, quinta Série A e sábado Copa, às 18h. O playoff de acesso é no sábado seguinte ao último jogo (que é a final da Copa, 23/01), por isso 30/01.

            Datas: Série A R1 28/11, R2 03/12 ... R8 14/01, R9 21/01 (só a A joga nessa semana). Série B R1 01/12 ... R7 12/01. Copa: fase de liga 05/12, 12/12, 19/12, 26/12; playoff 02/01; quartas 09/01; semis 16/01; final 23/01. Não tem mais a semana em que a B folgava e a A jogava duas vezes.

            Datas que caem em feriado: Série A R5 em 24/12, Série A R6 em 31/12, Copa R4 em 26/12 e playoff da Copa em 02/01. Cada rodada pode ter a data mudada à mão na aba Rodadas da competição; rodada com jogo começado não é remarcada.
            """;

        private const string ResumoCalendario =
            "Supercopa na quinta 26/11, Série A estreia no sábado 28/11 e depois é terça Série B, quinta Série A e sábado Copa.";

        private const string SiteCalendario =
            """
            A temporada 2011 abre com a *Supercopa* e segue um ritmo fixo durante a semana. O calendário completo, rodada por rodada, está na página *Formato*.

            ## A abertura
            - *Quarta 25/11*: fecha a janela de transferências.
            - *Quinta 26/11*: Supercopa, campeão da Série A x campeão da Copa.
            - *Sábado 28/11*: 1ª rodada da Série A.

            ## O ritmo da semana
            A partir da semana seguinte:
            - *Terça*: Série B
            - *Quinta*: Série A
            - *Sábado*: Copa

            > Exemplo: na semana de 30/11, a Série B joga na terça 01/12, a Série A na quinta 03/12 e a Copa estreia no sábado 05/12.

            ## Como termina
            - A Série B tem 7 rodadas e termina na terça 12/01.
            - A Série A tem 9 rodadas e termina na quinta 21/01.
            - A final da Copa é no sábado 23/01.
            - O playoff de acesso fecha a temporada no sábado 30/01.
            """;

        // ── Copa ────────────────────────────────────────────────────────────────────────────

        private const string TextoCopa =
            """
            *Novidade: a Copa agora é em fase de liga* 🏆

            Acabaram os grupos. Os 18 times ficam numa *tabela só*, como na Champions, e cada um faz *4 jogos* (2 em casa e 2 fora).

            - *1º ao 4º*: direto para as quartas.
            - *5º ao 12º*: playoff (5º x 12º, 6º x 11º, 7º x 10º, 8º x 9º).
            - *13º ao 18º*: eliminados.

            *O sorteio é equilibrado*: sai do ranking de 2010 (posição na liga + campanha na Copa). Os 9 melhores vão ao pote 1 e os outros ao pote 2; cada time pega 2 adversários de cada pote, e o sorteio escolhe uma tabela em que todo mundo tem adversários de força parecida.

            O sorteio é *ao vivo* no site, na página *Sorteio da Copa*.

            Fase de liga nos sábados 05/12, 12/12, 19/12 e 26/12; playoff 02/01; quartas 09/01; semis 16/01; final 23/01.

            Quem cair no playoff ganha *5 Mi* de premiação.
            """;

        private const string ExplicacaoCopa =
            """
            Formato (Liga.FormatoCopa = fase de liga; Copas antigas continuam com grupos). Todos os times das Séries A e B da temporada entram.

            Ranking do sorteio: posição geral em 2010 (Série A 1º a 10º, depois Série B 11º a 18º, a B pela regra de desempate dela) menos o bônus da Copa 2010: campeão 4 posições, vice 3, semifinal 2, quartas 1. Empate: quem foi melhor na liga fica na frente. Com os dados de 2010: pote 1 = Flamengo, Grêmio, Vasco, Botafogo, Coritiba, Santos, Fluminense, São Paulo e Palmeiras; pote 2 = Corinthians, Cruzeiro, Internacional, Náutico, Paraná, Sport, Vitória, Juventude e Figueirense. Confira em Admin › Liga › Copa › aba Sortear Copa antes de publicar.

            Sorteio: cada time faz 2 jogos contra o próprio pote e 2 contra o outro, 2 em casa e 2 fora, um por rodada, sem repetir adversário. O site gera centenas de tabelas válidas e fica com uma das mais equilibradas: a média da posição no ranking dos 4 adversários de cada time fica parecida (nos testes, diferença de no máximo 1 posição entre a tabela mais difícil e a mais fácil). O admin clica em "Sortear e revelar ao vivo"; os jogos já ficam gravados com as datas e a página /copa/sorteio revela um time a cada 10 segundos (pote 1 primeiro) com os 4 adversários. Dá para sortear de novo enquanto a Copa não começar.

            Desempate da tabela: Pontos → Vitórias → Saldo → Gols Pró. Se ainda empatar bem na linha (4º/5º ou 12º/13º), jogo decisivo (não vale pontos). Empate total dentro da mesma faixa não tem jogo extra.

            Mata-mata, jogo único com mando do melhor colocado: playoff 5x12, 6x11, 7x10, 8x9; quartas 1º x vencedor de 8/9, 2º x 7/10, 3º x 6/11, 4º x 5/12; semis 1/8/9 x 4/5/12 e 2/7/10 x 3/6/11; final.

            Premiação (copiada de 2010 ao criar a de 2011, com o playoff incluído): campeão 35 Mi, vice 20, semifinal 10, quartas 6, playoff 5, primeira fase 4.

            Outros efeitos: metas da diretoria na Copa pelo ranking (1º a 4º meta de semifinal, 5º a 8º de quartas, o resto sem meta); loteria do draft (Round 2): 18º ao 14º disputam as picks 1–5 e o 13º com os 4 eliminados no playoff as picks 6–10; amarelos zeram no primeiro jogo de mata-mata do time (o playoff, para quem ficou de 5º a 12º).
            """;

        private const string ResumoCopa =
            "Os 18 times numa tabela só, 4 jogos cada: 1º ao 4º vão às quartas, 5º ao 12º jogam o playoff. Sorteio equilibrado e ao vivo.";

        private const string SiteCopa =
            """
            A Copa deixou de ter grupos. Agora é uma *fase de liga*, como na Champions: os 18 times ficam numa *tabela só* e cada um faz *4 jogos*, 2 em casa e 2 fora, contra adversários sorteados.

            ## Quem passa
            - *1º ao 4º*: vão direto para as quartas de final.
            - *5º ao 12º*: jogam o playoff, em jogo único com mando do melhor colocado.
            - *13º ao 18º*: estão eliminados.

            No playoff é 5º x 12º, 6º x 11º, 7º x 10º e 8º x 9º. Quem vence pega um dos 4 primeiros nas quartas:
            - 1º x vencedor de 8º x 9º
            - 2º x vencedor de 7º x 10º
            - 3º x vencedor de 6º x 11º
            - 4º x vencedor de 5º x 12º

            > Exemplo: quem termina em 1º enfrenta nas quartas o vencedor do jogo entre o 8º e o 9º.

            ## Como funciona o sorteio
            1. Os times são ordenados pelo *ranking de 2010*: a posição geral na liga (Série A antes da B) e a Copa, que faz o time subir (campeão 4 posições, vice 3, semifinal 2, quartas 1).
            2. Os 9 primeiros formam o *pote 1* e os outros 9 o *pote 2*.
            3. Cada time pega *2 adversários do pote 1 e 2 do pote 2*, sem repetir adversário.
            4. Entre as tabelas possíveis, o site escolhe uma em que a *força média dos adversários é parecida para todo mundo*. Ninguém fica com uma tabela muito mais difícil que a dos outros.

            > Exemplo: o Corinthians foi 2º da Série B e campeão da Copa, então sobe 4 posições no ranking (do 12º para o 8º no índice), empata com São Paulo e Palmeiras e fica atrás deles por ter ido pior na liga: termina em 10º no ranking, no pote 2.

            O sorteio é *ao vivo* na página *Sorteio da Copa*: um time a cada 10 segundos, primeiro o pote 1, cada um com os seus 4 adversários.

            ## Desempate
            Pontos, Vitórias, Saldo de Gols e Gols Pró. Se o empate continuar bem na linha (4º/5º ou 12º/13º), a vaga sai num *jogo decisivo*, que não vale pontos.

            ## Datas
            - Fase de liga: sábados 05/12, 12/12, 19/12 e 26/12
            - Playoff: 02/01
            - Quartas: 09/01
            - Semifinais: 16/01
            - Final: 23/01

            ## Premiação
            Campeão 35 Mi, vice 20 Mi, semifinal 10 Mi, quartas 6 Mi, *playoff 5 Mi* (novo) e primeira fase 4 Mi.
            """;
    }
}
