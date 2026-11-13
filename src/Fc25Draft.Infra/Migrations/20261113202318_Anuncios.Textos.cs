using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <summary>
    /// Os anúncios que estavam em docs/anuncios-temporada-2011.md, já com a explicação de cada regra,
    /// para a tela /admin/anuncios nascer com eles.
    /// </summary>
    public partial class Anuncios
    {
        private const string GrupoJaValendo = "Novidades já valendo";
        private const string GrupoVirada2011 = "Virada para 2011";

        private static void InserirAnunciosIniciais(MigrationBuilder migrationBuilder)
        {
            var outubro = new DateTime(2026, 10, 31, 12, 0, 0);
            var novembro = new DateTime(2026, 11, 5, 21, 0, 0);

            Inserir(migrationBuilder, "6f2b8c1e-3d4a-4b5c-9e1f-0a1b2c3d4e01", GrupoJaValendo, 1, "Visual novo do site", novembro,
                """
                *O site da CBFV está de cara nova* 🎨

                Todas as páginas foram redesenhadas: Liga, Mercado, times, jogadores, Draft, Bolão, Álbum, Prêmios, História e o resto. Nada mudou nas regras nem nos seus dados, só a forma de ver.

                *O que muda na prática*
                - Fundo grafite e o dourado só no que importa: o líder, a sua vez, o campeão, a escolha que acabou de sair.
                - A forma de cada time aparece em quadradinhos de *V/E/D*, na tabela, no time e no Power Ranking.
                - Escudo do lado de cada time, em todas as listas e tabelas.
                - O overall dos jogadores vem na cor do PES, como no jogo.
                - O *modo escuro* ficou bem melhor (os times de camisa preta agora aparecem). O botão de sol e lua no topo troca entre claro e escuro.
                - No celular, tudo cabe na tela, sem precisar arrastar para o lado.
                - O telão do draft e o telão do jogo também ganharam o visual novo.

                Achou alguma tela estranha, algo cortado ou difícil de ler? Manda um print aqui no grupo que a gente arruma.
                """,
                """
                Só a aparência mudou: nenhuma regra, cálculo ou dado foi alterado. Se alguém disser que "o valor mudou" ou "a tabela está diferente" por causa do visual, não foi o redesenho.

                Onde o dourado aparece: líder da tabela, a vez do técnico no draft, campeão, a escolha que acabou de sair. Fora isso, tudo fica em tons de grafite de propósito.

                Quadradinhos V/E/D: a sequência dos últimos jogos do time (vitória, empate, derrota), na classificação, na página do time e no Power Ranking.

                Cores do overall: as mesmas faixas do PES (90+, 80+, 70+...), para bater com o que se vê no jogo.

                Telão do draft: fica sempre escuro, mesmo com o site no modo claro, porque é feito para passar na TV.

                Coisas que ficaram iguais de propósito: as figurinhas e a abertura de pacote do Álbum, o card do Craque da partida (roxo das lendárias) e os emojis de humor dos jogadores (😤 e 😐).

                Se chegar reclamação de tela quebrada, pedir: print, qual página, se é celular ou computador e se está no claro ou no escuro.
                """);

            Inserir(migrationBuilder, "6f2b8c1e-3d4a-4b5c-9e1f-0a1b2c3d4e02", GrupoJaValendo, 2, "Picks negociáveis", outubro,
                """
                *Novidade no site da CBFV: Picks do draft nas negociações* 🔄

                A partir de agora, as picks do próximo draft (1ª e 2ª rodada de cada time) entram nas propostas entre times. Dá para pedir a pick de um rival, oferecer a sua, ou misturar com jogadores e dinheiro.

                A pick vale a posição que o time dono dela pela classificação tirar na loteria. Quando o draft é gerado, quem escolhe é o dono atual. Comprou a pick de um time que está mal na tabela? Pode ter comprado uma escolha alta.

                *Negócio só de picks* (com ou sem dinheiro) vale *mesmo com a janela fechada* e não conta no limite de transferências.

                A pick se negocia até ser usada no draft. A página *Picks do Draft* mostra quem é dono de cada pick negociada, e cada troca vira notícia no Plantão.

                Para fazer, é só ir em *Negociações* no seu time e montar a proposta.
                """,
                """
                Quais picks: a 1ª e a 2ª rodada do próximo draft de cada time. A posição da pick é a que o time de origem (o da classificação) tirar na loteria, não a do time que comprou.

                Dono: pick que nunca foi negociada é do próprio time. Quando é negociada, o site guarda o dono atual; a página Picks do Draft mostra só as que mudaram de mão.

                Janela e limite:
                - Proposta só com picks (com ou sem dinheiro): vale com a janela fechada e não conta no limite de transferências.
                - Se tiver jogador junto, vale a regra normal: precisa da janela aberta e conta no limite.
                - Empréstimo não pode levar pick.

                Quando uma proposta com uma pick é aceita, as outras propostas pendentes que tinham essa mesma pick são canceladas sozinhas.

                Na hora de gerar o draft, a vaga da pick vai para o dono atual, não para o time de origem.

                Cada troca vira notícia no Plantão (🔄). A regra completa está no Manual do Técnico, seção 5.
                """);

            Inserir(migrationBuilder, "6f2b8c1e-3d4a-4b5c-9e1f-0a1b2c3d4e03", GrupoJaValendo, 3, "Picks no dia do draft e pick protegida", outubro,
                """
                *Novidades nas picks do draft* 🔄🛡️

                *Negociar posição até a hora de escolher*
                A negociação de picks não fecha mais na loteria. Depois dela, cada pick já mostra a posição (#3, #8...) e continua negociável até ser usada no draft, inclusive com o draft rolando. Quer subir para pegar aquele jogador? Oferece a sua #8 e mais alguma coisa pela #3. Não gostou das opções? Desce e faz caixa.

                *Pick protegida*
                Antes da loteria, quem cede uma pick pode protegê-la: por exemplo, "protegida até a #3", com uma devolução em dinheiro combinada. Se ela cair dentro da proteção, volta para quem protegeu, e quem tinha a pick recebe a devolução. Se cair depois, fica com quem comprou. A conta é feita sozinha assim que a loteria termina.

                Tudo isso fica em *Negociações* (na proposta) e na página *Picks do Draft*.
                """,
                """
                Até quando negocia: até a pick ser usada (o jogador escolhido). Com o draft rolando, a troca muda na hora o time que escolhe naquela posição.

                Depois da loteria, cada pick aparece com o número da posição (#3, #8...).

                Virada no meio do draft: continuam negociáveis as picks livres do draft da temporada atual e as de um draft da temporada anterior que ainda não terminou.

                Pick protegida:
                - Só dá para proteger antes da loteria, e quem protege é quem está cedendo a pick.
                - Na proposta vão dois números: "protegida até a #N" e o valor da devolução.
                - Quando a loteria termina, o site resolve sozinho: se a pick cair na #N ou antes, ela volta para quem protegeu, e quem protegeu paga a devolução a quem estava com a pick. Se cair depois da #N, fica com quem comprou e ninguém paga nada.
                - Depois da loteria a proteção já foi resolvida; a pick segue negociável normalmente, sem proteção.
                """);

            Inserir(migrationBuilder, "6f2b8c1e-3d4a-4b5c-9e1f-0a1b2c3d4e04", GrupoJaValendo, 4, "Cláusula de recompra", outubro,
                """
                *Novidade no site da CBFV: Cláusula de recompra* 🔁

                Agora, na venda de um jogador para outro time, a proposta pode ter um *preço de recompra*. Quem vende ganha o direito de comprar o jogador de volta por esse valor até o fim da temporada.

                É uma troca de risco: quem compra paga menos porque aceita que pode perder o jogador, e quem vende faz caixa agora sem largar o jogador de vez.

                Algumas regras:
                - Vale só na venda de um jogador (não em troca nem em empréstimo).
                - A recompra vale enquanto o jogador estiver no clube que comprou. Se ele for vendido ou emprestado, a cláusula cai.
                - Recomprar é uma transferência: precisa da janela aberta e conta no limite.

                As cláusulas ficam em *Negociações → Empréstimos e recompras*, e a ficha do jogador mostra quando ele tem uma.
                """,
                """
                Quando dá para pôr: só em venda (jogador por dinheiro) com exatamente um jogador. Troca, empréstimo ou venda de dois ou mais jogadores não aceitam cláusula.

                O direito nasce quando a venda é aceita e vale até a virada da temporada. Ele cai se:
                - o jogador sair do clube que comprou (vendido ou emprestado);
                - a temporada virar;
                - a recompra já tiver sido feita.

                Recomprar: quem vendeu aperta o botão e a recompra entra como negócio já aceito. O clube que comprou não pode recusar. Precisa da janela aberta, conta no limite de transferências e o preço sai do caixa de quem recompra para o de quem tinha comprado.

                Onde ver: Negociações → Empréstimos e recompras (dos dois lados), e o selo 🔁 na ficha do jogador. A regra também está no Manual do Técnico.
                """);

            Inserir(migrationBuilder, "6f2b8c1e-3d4a-4b5c-9e1f-0a1b2c3d4e05", GrupoVirada2011, 1, "Papel no elenco", outubro,
                """
                *Novidade na temporada 2011: Papel no elenco* ⭐

                A partir de agora, cada jogador tem um papel no elenco, que define quanto ele espera jogar:

                ⭐ *Titular*: reclama se jogar menos de 50% dos últimos 6 jogos.
                🔁 *Disputa*: reclama com 25% ou menos.
                🪑 *Rotação*: sabe que é reserva, só reclama se não tiver nenhuma chance.
                🌱 *Promessa*: não reclama na temporada, e o valor dele não cai.

                Disputa e rotação só reclamam se estiverem entre os melhores da posição no elenco. Se quem joga na frente é melhor, ele entende. E enquanto o jogador cumpre o papel, o valor de mercado não cai pela participação.

                *Como o papel é definido*
                - Leilão: pelo quanto você pagou sobre o lance inicial. Até 25% a mais é rotação, acima disso é disputa, e 75% a mais (ou o comprar já) é titular.
                - Draft, 1ª rodada: quem chega entre os melhores da posição vem como titular (primeira metade da rodada) ou disputa (segunda metade). Se já tem gente melhor na posição, vem como rotação.
                - Draft, 2ª rodada em diante: jovem de até 23 anos chega como promessa.
                - Troca ou venda entre times: disputa ou rotação, pela posição.
                - Na virada, o papel de todo mundo é refeito pelo que jogou na temporada: 60% ou mais vira titular, de 25% a 60% disputa, e menos de 25% rotação. Com 33 anos ou mais, no máximo disputa. Com 21 ou menos e sem ser titular, promessa.

                *E mais*
                😐 Um jogo antes de ficar insatisfeito, o jogador fica *incomodado* e você recebe o aviso.
                🗣️ *Conversa*: uma por temporada. Desce o papel de um jogador um degrau (titular para disputa, disputa para rotação), e o valor dele cai 5% pelos próximos 5 jogos. Não vale para quem já pediu para sair.
                🏠 *Prata da casa*: quem está há 2 temporadas completas no clube tem mais paciência e olha os últimos 8 jogos em vez de 6.

                O papel de cada jogador aparece no elenco e na ficha. Detalhes no Manual do Técnico.
                """,
                """
                Quando liga: na virada para 2011. A virada grava o papel de todos os jogadores; quem chega ao clube depois tem o papel gravado sozinho em até um minuto. Jogos de antes da virada continuam contando pela regra antiga.

                Quando fica insatisfeito (😤), olhando os últimos 6 jogos do clube:
                - ⭐ Titular: jogou menos de 50%.
                - 🔁 Disputa: jogou 25% ou menos.
                - 🪑 Rotação: jogou 10% ou menos.
                - 🌱 Promessa: nunca.

                "Entre os melhores da posição": as posições são agrupadas em goleiro, zaga, lateral, meio e ataque. Conta quantos titulares o time usa em cada grupo (média das últimas 8 escalações; sem escalação, 1/2/2/3/3). Disputa e rotação só reclamam se estiverem entre esses N melhores do grupo. Titular reclama sempre.

                Valor de mercado: enquanto o jogador joga o que o papel pede (50% titular, 25% disputa, 10% rotação), o valor não cai pela participação. Abaixo disso, entra a regra normal do valor por participação.

                Leilão: conta o que foi pago dividido pelo lance inicial. Até 1,25× é rotação; acima é disputa; 1,75× ou mais (ou comprar já) é titular.

                Draft de 1ª rodada: se o jogador entra entre os melhores da posição no time, vem titular (primeira metade da rodada) ou disputa (segunda metade). Se não entra, rotação. Da 2ª rodada em diante: promessa só com até 23 anos; mais velho (e draft de expansão) vai pela posição.

                Virada: refaz pelo que jogou na temporada que acabou (60%+ titular, 25%+ disputa, menos rotação). 33 anos ou mais: no máximo disputa. 21 ou menos e não titular: promessa.

                Conversa: uma por time por temporada. Desce um degrau, o valor cai 5% pelos próximos 5 jogos do clube. Não dá para usar em quem já pediu para sair.

                Prata da casa: duas temporadas completas no clube (chegou até 7 dias depois do início da temporada conta como inteira). A janela passa de 6 para 8 jogos.

                Não há botão para ligar antes da virada.
                """);

            Inserir(migrationBuilder, "6f2b8c1e-3d4a-4b5c-9e1f-0a1b2c3d4e06", GrupoVirada2011, 2, "Bolão que paga o clube", outubro,
                """
                *Novidade na temporada 2011: o bolão agora paga o seu clube* 💰

                Quem fizer mais pontos na rodada leva *R$ 2 mi* para o caixa do clube que treina. Empate em pontos e cravadas divide o prêmio.

                No fim da temporada, o ranking geral também paga:
                🥇 *R$ 15 mi*
                🥈 *R$ 10 mi*
                🥉 *R$ 5 mi*

                Técnico e auxiliar jogam separado, e cada um ganha o seu. Quem está sem clube continua jogando, mas não recebe.

                O dinheiro cai no caixa assim que a rodada termina (o mata-mata da Copa paga quando a Copa acaba) e aparece no extrato do time. Quem fica sem palpitar está deixando dinheiro na mesa.
                """,
                """
                Vale a partir da temporada 2011. Rodadas de antes não pagam.

                Prêmio da rodada: R$ 2 mi para quem fizer mais pontos. Paga quando todos os jogos da rodada estão encerrados. O mata-mata da Copa conta como uma rodada só e paga quando a Copa termina.

                Prêmio do geral: 1º R$ 15 mi, 2º R$ 10 mi, 3º R$ 5 mi. Paga quando a temporada acaba: a Supercopa dela já existe e todas as competições estão encerradas.

                Empate (mesmos pontos e mesmas cravadas): soma os prêmios das colocações empatadas e divide. Ex.: dois empatados em 1º no geral levam (15 + 10) ÷ 2 = R$ 12,5 mi cada, e o próximo fica em 3º.

                Para onde vai: para o clube que o técnico ou auxiliar treina na hora do pagamento. Sem clube, não recebe (e o prêmio não passa para o próximo). Quem fez zero ponto não ganha nada. Técnico e auxiliar do mesmo clube podem ganhar os dois: o clube recebe duas vezes.

                Onde ver: extrato do time (origem Bolão) e aviso 🎯 para o técnico.
                """);

            Inserir(migrationBuilder, "6f2b8c1e-3d4a-4b5c-9e1f-0a1b2c3d4e07", GrupoVirada2011, 3, "Comissão técnica", outubro,
                """
                *Novidade na temporada 2011: Comissão técnica* 💼

                Na aba *Treino* do seu time, agora dá para contratar profissionais que valem a temporada inteira. O pagamento sai do caixa na hora, e a contratação só pode ser feita nas *3 primeiras semanas da temporada*.

                🌱 *Treinador da base* (R$ 8 mi): o treino da semana passa a ter 4 vagas e aceita jogadores de até 26 anos.
                💪 *Preparador físico* (R$ 6 mi): os 3 jogadores mais velhos do elenco caem 1 a menos na evolução da virada.
                🧤 *Treinador de goleiros* (R$ 5 mi): o goleiro ganha +1 a mais em cada semana de treino (não soma com o padrinho).

                Cada cargo pode ser contratado uma vez por temporada, e não tem devolução. Pense bem: o dinheiro que vai para a estrutura é o que deixa de ir para o leilão.

                Detalhes no Manual do Técnico.
                """,
                """
                Vale a partir da temporada 2011.

                Prazo: 21 dias contados do início da Série A da temporada. O card da aba Treino mostra até quando dá; depois, aparece "prazo de contratação encerrado".

                Pagamento: sai do caixa na hora da contratação (extrato com origem Comissão). Sem caixa suficiente, não contrata. Não tem devolução. O cargo é do clube, não do técnico.

                Cada cargo, uma vez por temporada por clube. Na virada, todos os cargos acabam e precisam ser contratados de novo.

                Treinador da base: o treino da semana normal tem 3 vagas para jogadores de até 25 anos e overall até 85. Com ele, são 4 vagas e a idade vai até 26 (o limite de overall continua 85).

                Preparador físico: na evolução da virada, os 3 jogadores mais velhos do elenco perdem 1 ponto a menos do que perderiam.

                Treinador de goleiros: o goleiro no treino ganha +1 a mais por semana. Não soma com o padrinho: se o goleiro já tem o +1 do padrinho, fica só com um deles.
                """);
        }

        private static void Inserir(MigrationBuilder migrationBuilder, string id, string grupo, int ordem, string titulo,
            DateTime criadoEm, string texto, string explicacao)
        {
            migrationBuilder.InsertData(
                table: "Anuncios",
                columns: new[] { "AnuncioId", "Grupo", "Titulo", "Texto", "Explicacao", "Ordem", "EnviadoEm", "CriadoEm", "AtualizadoEm" },
                values: new object[] { Guid.Parse(id), grupo, titulo, texto.Replace("\r\n", "\n"), explicacao.Replace("\r\n", "\n"), ordem, null, criadoEm, criadoEm });
        }
    }
}
