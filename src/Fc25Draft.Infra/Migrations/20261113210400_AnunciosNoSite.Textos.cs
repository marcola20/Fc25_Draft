using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <summary>
    /// Resumo e texto do site (mais explicado, com exemplos) dos 7 anúncios que vieram do .md.
    /// Nenhum é publicado aqui: quem publica é o admin, no botão da tela /admin/anuncios.
    /// </summary>
    public partial class AnunciosNoSite
    {
        private static void PreencherTextosDoSite(MigrationBuilder migrationBuilder)
        {
            Preencher(migrationBuilder, "6f2b8c1e-3d4a-4b5c-9e1f-0a1b2c3d4e01",
                "Todas as páginas foram redesenhadas. Nenhuma regra mudou: veja o que ficou diferente na forma de ver.",
                """
                O site da CBFV ganhou uma cara nova. Liga, Mercado, times, jogadores, Draft, Bolão, Álbum, Prêmios, História: todas as páginas foram redesenhadas.

                *Nada mudou nas regras nem nos seus dados.* Valores, tabela, elenco, caixa: tudo continua igual. Só mudou a forma de ver.

                ## O dourado é o destaque
                O site agora é em tons de grafite, e o dourado aparece só no que merece atenção:
                - o líder da tabela;
                - a sua vez no draft;
                - o campeão;
                - a escolha que acabou de sair no draft.

                Se você está vendo dourado, é porque ali tem algo importante.

                ## A forma do time em quadradinhos
                Na classificação, na página do time e no Power Ranking, cada time tem uma fileira de quadradinhos com os últimos resultados: *V* (vitória), *E* (empate) e *D* (derrota).

                > Exemplo: um time com V V E V D venceu três, empatou um e perdeu um dos últimos cinco jogos. Bateu o olho, viu a fase do time.

                ## Escudos e overall
                - Todo time aparece com o escudo do lado, em todas as listas e tabelas.
                - O overall dos jogadores vem com as mesmas cores do PES. Um 90 no site tem a mesma cor que tem no jogo.

                ## Modo escuro
                O botão de sol e lua no topo troca entre claro e escuro. O modo escuro ficou bem melhor: os times de camisa preta, que sumiam no fundo, agora aparecem.

                ## No celular
                Tudo cabe na tela. Tabelas e listas foram ajeitadas para não precisar arrastar para o lado.

                ## Os telões
                O telão do draft e o telão do jogo também ganharam o visual novo. O telão do draft fica sempre escuro, mesmo com o site no claro, porque foi feito para passar na TV.

                ## Achou algo estranho?
                Alguma tela cortada, difícil de ler ou fora do lugar? Manda um print no grupo dizendo qual página, se foi no celular ou no computador, e se estava no claro ou no escuro. A gente arruma.
                """);

            Preencher(migrationBuilder, "6f2b8c1e-3d4a-4b5c-9e1f-0a1b2c3d4e02",
                "As picks do próximo draft entram nas propostas entre times. Negócio só de picks vale até com a janela fechada.",
                """
                As picks do próximo draft agora são moeda de negociação. Dá para pedir a pick de um rival, oferecer a sua, ou misturar picks com jogadores e dinheiro na mesma proposta.

                ## Quais picks entram
                Cada time tem duas picks negociáveis: a da *1ª rodada* e a da *2ª rodada* do próximo draft.

                ## Quanto vale uma pick
                A posição da pick é a que o *time de origem* tirar na loteria, pela classificação dele. Não importa quem é o dono na hora do sorteio.

                > Exemplo: você está brigando lá em cima e o Time X está na lanterna. Você compra a 1ª rodada do Time X. Se o Time X tirar a #1 na loteria, quem escolhe primeiro no draft é você.

                Comprar a pick de quem está mal na tabela é apostar numa escolha alta. Vender a sua quando está bem na tabela é trocar uma escolha baixa por algo que ajuda agora.

                ## Janela fechada? Pode negociar
                Proposta *só com picks* (com ou sem dinheiro):
                - vale mesmo com a janela de transferências fechada;
                - não conta no limite de transferências.

                > Exemplo: janela fechada, você oferece R$ 8 mi pela 2ª rodada de um rival. Pode mandar e, se ele aceitar, o negócio fecha na hora.

                Se a proposta tiver *jogador* junto, vale a regra normal: precisa da janela aberta e conta no limite.

                ## Como fazer
                1. Vá em *Negociações* no seu time.
                2. Monte a proposta como sempre e escolha as picks que quer dar ou pedir.
                3. Mande. Quando o outro técnico aceitar, a pick muda de dono.

                ## Bom saber
                - Empréstimo não pode levar pick.
                - Se duas propostas pedem a mesma pick e uma é aceita, a outra é cancelada sozinha.
                - Quando o draft é gerado, quem escolhe é o dono atual da pick.
                - A página *Picks do Draft* mostra quem é dono de cada pick que já mudou de mão, e cada negócio vira notícia no Plantão.
                """);

            Preencher(migrationBuilder, "6f2b8c1e-3d4a-4b5c-9e1f-0a1b2c3d4e03",
                "A pick agora se negocia até ser usada, com o draft rolando. E dá para ceder uma pick protegida.",
                """
                Duas novidades nas picks do draft: dá para negociar a posição até a hora de escolher, e dá para ceder uma pick com proteção.

                ## Negociar até a hora de escolher
                A negociação de picks não fecha mais na loteria. Depois do sorteio, cada pick já mostra a posição (#3, #8...) e continua negociável até ser usada, inclusive com o draft rolando.

                > Exemplo: o draft está andando e o jogador que você quer vai sair antes da sua vez. Você tem a #8 e oferece a #8 mais R$ 5 mi pela #3. Se o dono da #3 aceitar, você escolhe na #3.

                > Exemplo: não gostou de quem sobrou? Troque a sua #4 pela #10 de outro time e mais um dinheiro. Você desce algumas posições e faz caixa.

                ## Pick protegida
                Antes da loteria, quem *cede* uma pick pode protegê-la. Na proposta vão duas coisas:
                - até qual posição ela está protegida (por exemplo, "protegida até a #3");
                - uma devolução em dinheiro, combinada entre os dois.

                Quando a loteria termina, o site resolve sozinho:
                - a pick caiu *dentro da proteção*: ela volta para quem protegeu, e quem protegeu paga a devolução para quem estava com ela;
                - a pick caiu *depois da proteção*: fica com quem comprou, e ninguém paga nada.

                > Exemplo: o Time A cede a 1ª rodada para o Time B, protegida até a #3, com devolução de R$ 8 mi.
                > Se o Time A tirar a #2, a pick volta para o Time A, e o Time A paga R$ 8 mi ao Time B.
                > Se o Time A tirar a #6, o Time B fica com a #6.

                Para quem cede, é um seguro: se o time for muito mal, não perde uma escolha de topo. Para quem compra, a devolução compensa o risco.

                ## Bom saber
                - Só dá para proteger antes da loteria.
                - Depois que a loteria resolve a proteção, a pick segue negociável normalmente, já sem proteção.
                - Tudo fica em *Negociações* (na proposta) e na página *Picks do Draft*.
                """);

            Preencher(migrationBuilder, "6f2b8c1e-3d4a-4b5c-9e1f-0a1b2c3d4e04",
                "Na venda de um jogador, quem vende pode guardar o direito de comprá-lo de volta até o fim da temporada.",
                """
                Agora, na venda de um jogador para outro time, a proposta pode ter um *preço de recompra*. Quem vende ganha o direito de comprar o jogador de volta por esse valor, até o fim da temporada.

                ## Por que usar
                É uma troca de risco que pode servir aos dois lados:
                - *quem vende* faz caixa agora sem perder o jogador de vez;
                - *quem compra* pode pagar menos, porque aceita que talvez tenha que devolvê-lo.

                > Exemplo: você precisa de dinheiro para o leilão e vende o seu atacante por R$ 20 mi, com recompra de R$ 28 mi. O outro time usa o jogador. Se lá na frente você quiser o atacante de volta, paga R$ 28 mi e ele volta, mesmo que o outro técnico não queira.

                ## Como funciona
                1. Em *Negociações*, monte uma venda de *um* jogador por dinheiro e preencha o preço de recompra.
                2. Se o outro time aceitar, a cláusula passa a valer.
                3. Até a virada da temporada, você pode recomprar pelo preço combinado.

                Recomprar é uma transferência: precisa da janela aberta, conta no limite de transferências e o valor sai do seu caixa. O clube que comprou não pode recusar.

                ## Quando a cláusula cai
                - Se o jogador sair do clube que comprou (vendido ou emprestado).
                - Na virada da temporada.
                - Depois que a recompra é feita.

                > Exemplo: você vendeu com recompra, mas o comprador repassou o jogador para um terceiro time. A cláusula acabou: para tê-lo de volta, só negociando do zero.

                ## Quando não dá para usar
                - Em troca (jogador por jogador).
                - Em empréstimo.
                - Em venda com mais de um jogador.

                As cláusulas ficam em *Negociações → Empréstimos e recompras*, e a ficha do jogador mostra quando ele tem uma.
                """);

            Preencher(migrationBuilder, "6f2b8c1e-3d4a-4b5c-9e1f-0a1b2c3d4e05",
                "Cada jogador tem um papel (titular, disputa, rotação ou promessa) que diz quanto ele espera jogar antes de reclamar.",
                """
                A partir da temporada 2011, cada jogador tem um *papel no elenco*. O papel diz quanto ele espera jogar. Quem joga o que espera fica tranquilo e não perde valor de mercado pela participação.

                ## Os quatro papéis
                - ⭐ *Titular*: espera jogar. Fica insatisfeito se jogar menos de 50% dos últimos 6 jogos.
                - 🔁 *Disputa*: briga por posição. Fica insatisfeito com 25% ou menos.
                - 🪑 *Rotação*: sabe que é reserva. Só reclama se praticamente não jogar (10% ou menos).
                - 🌱 *Promessa*: está aprendendo. Não reclama na temporada, e o valor dele não cai pela participação.

                Na conta, começar jogando vale 1 jogo e entrar do banco vale meio.

                > Exemplo: nos últimos 6 jogos, o jogador foi titular em 2 e entrou do banco em 1. Ele jogou 2 + ½ = 2,5 de 6, uns 42%.
                > Se ele é *titular*, está abaixo de 50%: vai reclamar.
                > Se é *disputa*, está acima de 25%: está tranquilo.

                ## Ele entende quando tem gente melhor
                Disputa e rotação só reclamam se estiverem entre os melhores da posição no seu elenco. Se quem joga na frente dele é melhor, ele aceita o banco. Titular reclama sempre.

                > Exemplo: seu time joga com 2 zagueiros. Seu 3º melhor zagueiro, em disputa, quase não jogou: ele não reclama, porque os dois na frente são melhores. Já se o seu 2º melhor zagueiro ficar no banco, ele cobra.

                ## Como o papel é definido
                *Leilão*: pelo quanto você pagou em cima do lance inicial.
                - até 25% a mais: rotação;
                - mais de 25% a mais: disputa;
                - 75% a mais ou mais (ou no comprar já): titular.

                > Exemplo: lance inicial de R$ 10 mi. Arrematou por R$ 12 mi: rotação. Por R$ 15 mi: disputa. Por R$ 18 mi ou no comprar já: titular.

                *Draft, 1ª rodada*: se o jogador chega entre os melhores da posição no seu time, vem como titular (se foi escolhido na primeira metade da rodada) ou disputa (segunda metade). Se você já tem gente melhor na posição, vem como rotação.

                *Draft, 2ª rodada em diante*: jovem de até 23 anos chega como promessa.

                *Troca ou venda entre times*: disputa ou rotação, conforme a posição dele no seu elenco.

                ## Na virada, o papel é refeito
                No fim da temporada, o papel de todo mundo é recalculado pelo que jogou:
                - 60% ou mais: titular;
                - de 25% a 60%: disputa;
                - menos de 25%: rotação.

                E dois ajustes de idade:
                - com 33 anos ou mais, no máximo disputa;
                - com 21 anos ou menos e sem ser titular, promessa.

                > Exemplos na virada: jogou 70%, vira titular. Jogou 80% mas tem 34 anos, fica disputa. Tem 20 anos e jogou 40%, vira promessa.

                ## O aviso antes da reclamação
                😐 Um jogo antes de ficar insatisfeito, o jogador fica *incomodado* e você recebe um aviso. É a hora de escalar, vender ou conversar.

                ## A conversa
                🗣️ Uma vez por temporada, você pode conversar com um jogador e descer o papel dele um degrau: titular vira disputa, disputa vira rotação. Ele passa a esperar menos minutos, mas o valor dele cai 5% pelos próximos 5 jogos do clube.

                > Exemplo: seu titular perdeu a vaga para um reforço. Em vez de deixá-lo reclamar, você conversa: ele vira disputa e só cobra se jogar 25% ou menos.

                Não dá para conversar com quem já pediu para sair.

                ## Prata da casa
                🏠 Quem está há 2 temporadas completas no clube tem mais paciência: olha os últimos 8 jogos em vez de 6 para decidir se reclama.

                O papel de cada jogador aparece no elenco e na ficha. A regra completa está no Manual do Técnico.
                """);

            Preencher(migrationBuilder, "6f2b8c1e-3d4a-4b5c-9e1f-0a1b2c3d4e06",
                "Quem fizer mais pontos na rodada leva R$ 2 mi para o clube, e o top 3 do geral leva até R$ 15 mi.",
                """
                A partir da temporada 2011, o bolão paga o clube que você treina. Palpite certo vira dinheiro no caixa.

                ## Prêmio da rodada
                Quem fizer mais pontos na rodada leva *R$ 2 mi* para o caixa do clube.

                Empate em pontos e em cravadas divide o prêmio.

                > Exemplo: você e outro técnico fizeram 14 pontos e 2 cravadas, e ninguém fez mais. Cada um leva R$ 1 mi.

                ## Prêmio do geral
                No fim da temporada, o ranking geral do bolão paga:
                - 🥇 1º lugar: *R$ 15 mi*
                - 🥈 2º lugar: *R$ 10 mi*
                - 🥉 3º lugar: *R$ 5 mi*

                Empate no geral soma os prêmios das posições empatadas e divide.

                > Exemplo: dois técnicos empatam em 1º. Eles somam o 1º e o 2º (R$ 15 mi + R$ 10 mi) e cada um leva R$ 12,5 mi. Quem vem logo atrás fica em 3º e leva R$ 5 mi.

                ## Quando o dinheiro cai
                - A rodada paga assim que todos os jogos dela terminam.
                - O mata-mata da Copa conta como uma rodada só e paga quando a Copa acaba.
                - O geral paga quando a temporada termina.

                O prêmio aparece no extrato do time, e você recebe um aviso.

                ## Quem recebe
                - Técnico e auxiliar jogam separado, e cada um ganha o seu. Se os dois do mesmo clube ganharem, o clube recebe duas vezes.
                - O dinheiro vai para o clube que você treina na hora do pagamento.
                - Quem está sem clube continua jogando, mas não recebe, e o prêmio não passa para o próximo.

                ## Vale a pena?
                O caixa médio de um time gira em torno de R$ 20 mi. Ganhar algumas rodadas na temporada já paga um bom reforço no leilão. Quem fica sem palpitar está deixando dinheiro na mesa.
                """);

            Preencher(migrationBuilder, "6f2b8c1e-3d4a-4b5c-9e1f-0a1b2c3d4e07",
                "Na aba Treino, dá para contratar treinador da base, preparador físico e treinador de goleiros para a temporada.",
                """
                A partir da temporada 2011, dá para investir na estrutura do clube. Na aba *Treino* do seu time, você contrata profissionais que valem a temporada inteira.

                ## Os três cargos
                🌱 *Treinador da base*: R$ 8 mi
                O treino da semana passa a ter 4 vagas em vez de 3, e aceita jogadores de até 26 anos em vez de 25. O limite de overall continua o mesmo (até 85).

                > Exemplo: sem ele, você treina 3 jovens de até 25 anos. Com ele, pode pôr um 4º, e esse 4º pode ser aquele meia de 26 anos que ficava de fora.

                💪 *Preparador físico*: R$ 6 mi
                Na virada da temporada, os 3 jogadores mais velhos do elenco saem 1 ponto melhor na evolução.

                > Exemplo: seu zagueiro de 34 anos cairia 3 pontos na virada. Com o preparador, cai 2.

                🧤 *Treinador de goleiros*: R$ 5 mi
                O goleiro que está no treino ganha +1 a mais por semana: +2 em vez de +1.

                Não soma com o padrinho. Se o goleiro já tem um padrinho dando o +1 a mais, o treinador de goleiros não acrescenta nada para ele.

                ## Regras da contratação
                - Só nas *3 primeiras semanas da temporada*. Depois disso, o card mostra que o prazo acabou.
                - O pagamento sai do caixa na hora. Sem dinheiro em caixa, não dá para contratar.
                - Cada cargo pode ser contratado uma vez por temporada.
                - Não tem devolução.
                - Na temporada seguinte, é preciso contratar de novo.

                ## Vale a pena?
                Depende do seu elenco. Muitos jovens bons para treinar? O treinador da base rende o ano todo. Elenco veterano? O preparador segura a queda na virada. Goleiro jovem e sem padrinho? O treinador de goleiros dobra o ganho dele.

                Pense bem: o dinheiro que vai para a estrutura é o que deixa de ir para o leilão. E o bolão agora também enche o caixa.
                """);
        }

        private static void Preencher(MigrationBuilder migrationBuilder, string id, string resumo, string textoSite)
        {
            migrationBuilder.UpdateData(
                table: "Anuncios",
                keyColumn: "AnuncioId",
                keyValue: System.Guid.Parse(id),
                columns: new[] { "Resumo", "TextoSite" },
                values: new object[] { resumo, textoSite.Replace("\r\n", "\n") });
        }
    }
}
