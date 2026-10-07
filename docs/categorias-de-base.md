# Categorias de base CBFV

Plano das categorias de base da liga. Faz parte do plano geral `docs/ciclo-de-vida-dos-jogadores.md` (idade,
evolução, queda e aposentadoria de todos os jogadores): a evolução das promessas segue a virada de temporada de lá. Cada fase é feita numa sessão nova, na branch
`feat/categorias-de-base`, e vai para a `producao` quando estiver redonda (o Render publica a `producao`).

> **Revisto em 07/10/2026:** o draft passou a ser **único e só de jovens** (idade máxima por draft, padrão 23) e a
> entrada de jovens virou a "nova geração" de `docs/ciclo-de-vida-dos-jogadores.md` (overall de entrada 80+, ajustado pelo
> admin; dá para criar jogadores que não estão no jogo). O "draft da base aos 17" e a evolução rumo a um teto ficam
> **substituídos** por lá (teto indefinido, curva G + desempenho). Este documento fica como histórico das ideias.

## A ideia em uma frase

O admin cadastra jovens promessas quando quiser (Pogba, Griezmann, Götze…), cada uma com a idade que ele
escolher; na temporada em que a promessa faz **17 anos** ela entra no **draft da base**, os clubes escolhem, e
as promessas **evoluem** temporada a temporada — mais rápido se jogarem e jogarem bem — rumo ao jogador que
elas viraram de verdade.

## Decisões já tomadas

| Tema | Decisão |
|---|---|
| A base é de quem | **Da liga.** O admin cadastra as promessas quando quiser; ninguém é dono delas até o draft. |
| Idade | **O admin define no cadastro** (pode ser diferente da idade real: Griezmann pode entrar com 16). O site guarda o ano de nascimento na liga (temporada − idade) e calcula a idade em cada temporada. O cadastro só aceita até **17 anos**. |
| Quando entra no draft | No **draft da base da temporada em que faz 17**. Mais novo fica na fila de "futuras promessas" (visível em `/base`) e entra sozinho quando chegar a hora. |
| Como chegam aos clubes | **Draft da base**, na ordem inversa da classificação (o pior escolhe primeiro). |
| Depois do draft | O garoto vira **jogador normal do elenco** do clube que escolheu (conta no elenco, pode ser negociado), com a marca de promessa e a evolução especial. |
| Evolução | **Mista:** cresce um pouco toda temporada pela idade e cresce mais se jogar (jogos como titular e nota média do PES). |
| Potencial | **Faixa aproximada visível** (estrelas e "pode chegar a 85–90"); o número exato fica escondido. |
| Primeira leva (temporada que vem) | Paul Pogba, Juanfran, Antoine Griezmann, Christian Eriksen, Andriy Yarmolenko, Erik Lamela, Shinji Kagawa, Mario Götze, Raheem Sterling, Paulo Dybala — com a idade que o admin escolher no cadastro. |

## Como a promessa nasce: o jogador real "rejuvenescido"

Nenhum desses jogadores existe no site ainda. A base do PES embutida no site (`pes-jogadores.json.gz`, usada
pela busca e pelo `BasePesService`) tem a **versão adulta** de cada um. A promessa nasce dela:

1. O admin escolhe o jogador na base do PES (busca que já existe em `/api/admin/players/pes`).
2. Define a **idade** (até 17; ex.: Pogba 17, Sterling 15) e o **overall de entrada** (ex.: 68). O site
   sugere o overall por uma tabela de idade × teto, ajustável. A temporada do draft sai da idade.
3. O site cria o `Player` (nome igual ao do PES, para casar as notas da importação) com os `PlayerAtributos`
   da versão adulta **rebaixados** até o overall de entrada — o inverso do `OverallPes.Evoluir`, puxando mais
   para baixo os atributos de físico/experiência e menos os de talento (a definir na Fase 1).
4. O **teto (potencial real)** é o overall da versão adulta no PES; o admin pode ajustar. A faixa visível sai
   dele com folga (ex.: teto 87 → "pode chegar a 84–90", 4 estrelas).
5. O rebaixamento vira uma `EvolucaoPes` pendente (com deltas negativos, `Motivo = "Base: entrada"`): o Editor
   PES aplica no save como já faz com a venda rápida, e o jogo e o site ficam iguais. (A idade no save o admin
   ajusta no editor; ver "A decidir".)

A versão adulta guardada vira o **alvo da evolução**: cada temporada a promessa recupera parte da diferença,
atributo por atributo, e se forma parecida com o jogador real.

## O draft da base

- Novo `DraftTipo.Base`. O pote do draft são as promessas que fazem 17 na temporada (e as de 17 que ficaram
  sem clube num draft anterior), não "todos os livres".
- **As promessas no pote não podem vazar** para os lugares que hoje tratam "sem elenco" como disponível: o
  draft normal (`DraftStateService.Livres`), a lista pré-draft (`DraftWishlistService`), o filtro
  `onlyAvailable` dos jogadores, a geração do mercado (`MarketItemGenerationService`, `MarketCycleGenerator`) e
  o draft de expansão. Um filtro só, no Core, usado em todos.
- Ordem: inversa da classificação da temporada que acabou — Série B do último ao primeiro, depois Série A do
  último ao primeiro (quem caiu e quem subiu pela posição final da sua série). O admin ajusta com as
  ferramentas de ordem que o draft já tem. Rodadas: o admin define (ex.: 1 por clube; com 10 promessas e 18
  clubes, os 10 piores escolhem).
- Reaproveita tudo do draft: relógio, escolha automática, telão `/draft/telao`, listas.
- A escolha grava o clube que draftou e a data (origem da promessa), mostrada na carreira do jogador.

## A evolução

Roda **na virada de temporada** (junto do `LigaTemporadaService.GerarProximaTemporadaAsync`, que hoje não mexe
em idade), com **prévia** para o admin conferir antes de confirmar.

- A idade das promessas sobe sozinha (é calculada do ano de nascimento na liga); quem faz 17 entra na fila do
  próximo draft da base. Para os demais jogadores, ver "A decidir".
- **Crescimento** (valores iniciais, ajustáveis em constantes):

  | Idade na temporada | Crescimento base |
  |---|---|
  | até 18 | +4 |
  | 19–20 | +3 |
  | 21–22 | +2 |
  | 23 | +1 (última evolução de base) |

  × **fator de jogo** pela temporada: titular em menos de 20% dos jogos do clube → ×0,5; 20–60% → ×1; mais de
  60% → ×1,3; nota média do PES ≥ 7 → mais ×1,2. Arredondado, nunca passa do teto.
- O ganho vira atributos com `OverallPes.Evoluir`, puxando primeiro os atributos que mais faltam em relação à
  versão adulta, e uma `EvolucaoPes` pendente (`Motivo = "Base: temporada 2010"`) que o editor aplica no save.
  O gráfico de evolução da ficha (`EvolucaoOverall`) já mostra `EvolucaoPes` sozinho.
- **Formatura**: aos 24 anos ou ao chegar no teto a promessa vira jogador comum (sem evolução especial),
  com selo "Cria da base CBFV" para sempre.

Dados de jogo: titulares de `LigaEscalacoesPartida`, entradas de `LigaEventos` (substituição) e notas de
`LigaNotasJogadores`, filtrados por `Rodada.Liga.Temporada` (o `JogadorHistoricoService` já agrega parecido).

## Telas

- **Ficha do jogador**: selo "Promessa da base", faixa de potencial em estrelas, idade, clube que draftou e a
  previsão da próxima evolução ("jogando assim, deve subir +3 a +5").
- **Página do clube**: seção "Crias da base".
- **/base** (pública): quem entra no próximo draft da base, a fila de futuras promessas (com a temporada em
  que cada uma entra), as escolhas dos drafts e o ranking de quem mais cresceu.
- **Admin `/admin/base`**: montar o pote (buscar na base do PES, idade, overall de entrada, teto), criar o
  draft da base, rodar a evolução da temporada (prévia + confirmar) e o histórico.

## Modelo de dados (primeira versão)

- `Promessa(PlayerId, CadastradaNaTemporada, AnoDeNascimento, OverallDeEntrada, Teto, FaixaMin, FaixaMax,
  AtributosAlvo (os 25 da versão adulta, CSV como em EvolucaoPes), PesId, DraftadaPorTimeId?, DraftadaEm?,
  FormadaEm?)` — uma por jogador, enquanto for promessa e depois como histórico.
- `EvolucaoDaBase(Id, PlayerId, Temporada, IdadeNaTemporada, JogosDoClube, JogosComoTitular, NotaMedia,
  Crescimento, OverallAntes, OverallDepois, EvolucaoPesId)` — o que entrou na conta de cada evolução.
- `DraftTipo.Base` (novo valor) e a origem da promessa no `DraftPick`.

## Fases

### Fase 1 — Pote da base
Entidade `Promessa`, migração; filtro único "está no pote da base" aplicado em todos os lugares que hoje
tratam jogador sem elenco como disponível; admin `/admin/base` para cadastrar promessas a partir da base do
PES (idade até 17 escolhida pelo admin, overall de entrada e teto sugeridos e ajustáveis; a temporada do draft
sai da idade); rebaixamento dos atributos com `EvolucaoPes` de entrada; selo e faixa de potencial na ficha do
jogador; página `/base` com quem entra no próximo draft e a fila de futuras promessas.
**Pronto quando:** no banco local o admin cadastra os 10 nomes da lista com idades variadas (alguns com 17,
outros mais novos), cada um com atributos rebaixados coerentes com o overall de entrada e uma `EvolucaoPes`
pendente; `/base` separa quem entra no próximo draft de quem fica na fila; nenhum aparece no draft normal, no
mercado nem na lista de livres; cadastrar com 18 é recusado.

### Fase 2 — Draft da base
`DraftTipo.Base`, pote do draft = promessas sem clube, ordem inversa da classificação, criação pelo admin,
escolha grava a origem; telão e relógio reaproveitados; carreira do jogador mostra "Draft da base 2011".
**Pronto quando:** um draft da base de teste no banco local distribui as promessas na ordem certa e elas
entram no elenco dos clubes.

### Fase 3 — Evolução
Cálculo da evolução da temporada (idade, fator de jogo, teto, atributos rumo à versão adulta), prévia e
confirmação no admin, idade +1, `EvolucaoPes` para o editor, `EvolucaoDaBase` com a conta, formatura;
previsão na ficha; ranking em `/base`.
**Pronto quando:** com jogos de teste no banco local, a prévia mostra o crescimento de cada promessa com a
conta explicada, confirmar grava as evoluções uma vez só, e o gráfico da ficha mostra o salto.

### Fase 4 — Experiência
Aviso no celular para o treinador ("Sua promessa Götze evoluiu +4: agora 76"), anúncio do pote antes do draft,
selo "Cria da base" permanente, Hall da Fama "Revelação da temporada" (quem mais cresceu), figurinha especial
de promessa no álbum seguinte.

## A decidir (perguntar antes da fase em que pesa)

- **Idade no save do PES**: o `EvolucaoPes` só leva atributos; a idade o admin acerta no editor, ou o editor
  passa a ler a idade do site. Fase 1.
- **Quantas rodadas no draft da base** e se clube pode passar a vez. Fase 2.
- Promessa de 17 que ninguém escolheu no draft: volta no draft seguinte ou vira jogador livre comum? Fase 2.
