# Álbum de figurinhas CBFV

Plano do álbum de figurinhas da liga. Cada fase é feita numa sessão nova, na branch
`feat/album-figurinhas`, e só vai para `producao` quando o álbum estiver redondo (o Render
publica a `producao`; a branch não afeta o site no ar).

## A ideia em uma frase

Todo treinador tem um álbum com as figurinhas dos jogadores de todos os clubes da CBFV; ganha
pacotinhos jogando (entrar no site, bolão, vitórias), abre, cola, troca as repetidas com os outros
treinadores e quem completa páginas e o álbum ganha selo e lugar no Hall da Fama.

## Decisões já tomadas

| Tema | Decisão |
|---|---|
| Quem entra no álbum | Os **elencos dos clubes da liga** no momento em que o álbum da temporada é lançado. Todo jogador entra, com ou sem foto. |
| Foto | A figurinha usa a foto do jogador (`/fotos/jogadores/{playerId}`); sem foto aparece a silhueta. Admin tem uma tela para ver quem está sem foto e completar. A meta é todo mundo com foto. |
| Dono das figurinhas | A **pessoa** (`Treinador`), não o clube — igual ao bolão. Treinador e auxiliar têm álbuns separados; quem troca de clube leva o álbum. |
| Como ganha pacote | **1 por dia** (resgatado no site) · **bolão: 1 a cada X pontos** · **vitória do seu time: 1 pacote**. O admin também pode dar pacotes (testes, premiação). |
| Prêmio | **Selo** por página completa e **Hall da Fama** (seção Colecionadores) para quem completa o álbum. Não mexe no caixa dos times. |

## Como o álbum é montado

- **Um álbum por temporada** (`Álbum CBFV 2010`). Ao lançar, o admin gera o álbum a partir dos
  elencos de hoje: a figurinha é uma *foto do momento* (nome, posição, overall e clube daquela
  data). Transferência depois disso não mexe no álbum. Álbuns antigos continuam visíveis como coleção.
- **Uma página por clube**, na ordem alfabética (ou da tabela da Série A e depois Série B):
  1. escudo do clube (figurinha brilhante);
  2. jogadores do elenco por posição (GOL, ZAG, LE, LD, VOL, MC, MEI, ME, PE, MD, PD, CA, SA —
     `PositionExtensions.ToPositionSigla`) e, dentro da posição, por overall.
- **Numeração corrida** (#001, #002…) como num álbum de verdade.
- **Clubes da liga**: os `LigaTimes` das ligas da temporada (Série A e Série B). Na falta delas,
  `Teams.Where(!IsAdmin)`. Elenco: `TeamRosters` (é o que MinhaArea e ValorElenco usam).

### Raridades (valores iniciais, ajustáveis em constantes)

| Raridade | Quais | Visual |
|---|---|---|
| Comum | todos os jogadores | figurinha normal |
| Brilhante | escudo de cada clube + os 3 maiores overalls de cada clube | borda dourada e brilho holográfico |
| Lendária | **os 10 melhores jogadores da temporada passada, escolhidos pelo admin** | holográfico animado + moldura especial + selo "Melhores de 2009" |

Uma figurinha tem uma raridade só (a mais alta que se aplicar).

**Escolha das lendárias:** na tela de gerar o álbum, o admin escolhe até 10 jogadores entre os que
estão no álbum (estão num elenco da liga no lançamento). A lista já vem **pré-preenchida com
sugestões** — maiores médias de nota do PES da temporada anterior (`LigaNotasJogadores`, com mínimo
de jogos) — e o admin troca quem quiser. A figurinha lendária ganha um texto curto opcional que o
admin escreve ("Artilheiro de 2009", "Campeão invicto com o Santos"), impresso no verso/rodapé.
Dá para ajustar as lendárias até o primeiro pacote ser aberto; depois disso ficam travadas.

### Pacotinho

- 5 figurinhas. Vagas 1 a 4: comum (com 5% de chance de virar brilhante); vaga 5: 85% brilhante,
  15% lendária. Sorteio no servidor, na hora de abrir (não na hora de ganhar).
- Repetidas acumulam: a primeira cópia vai direto para o álbum (marcada como **nova**), as outras
  vão para o monte de repetidas.

## Ganhar pacotes: livro-razão idempotente

Nada de colocar gancho em todo lugar que encerra jogo (são vários caminhos: `EncerrarPartidaAsync`,
`EncerrarPartidaComPenaltisAsync`, `AplicarWOAsync`, `EncerrarKnockoutJogoAsync`, `ResultadoPesService`).
Em vez disso, um **livro-razão de pacotes ganhos** com chave única por motivo, e um serviço em
segundo plano que reconcilia:

- `PacoteGanho(Id, TreinadorId, Origem, Chave, CriadoEm, AbertoEm?)` — índice único em
  `(TreinadorId, Chave)`. Reconciliar de novo nunca dá pacote em dobro.
- **Diário**: chave `diario:2026-10-03` (data de Brasília, `BrazilTime`). Resgatado num botão
  "Pegar o pacote do dia" no álbum e na Minha Área.
- **Vitória**: chave `vitoria:{partidaId}`. Para cada partida `Encerrada` (fase de pontos, Copa e
  mata-mata; WO conta), as pessoas com passagem no clube vencedor na data do jogo (treinador **e**
  auxiliar) ganham 1. Passagem na data: `Desde <= quando && (Ate == null || Ate >= quando)`.
- **Bolão**: chave `bolao:{temporada}:{n}`. Pontos do treinador na temporada (mesma conta do
  `BolaoService` / `BolaoPontuacao`) ÷ X = quantos pacotes já deveria ter; cria os que faltam.
  X inicial = **30 pontos** (≈ 3 placares cravados ou 6 resultados certos).
- **Admin**: chave `admin:{guid}` com motivo; registra em `AdminActionsLogs` (novo `AdminActionType`).
- Só conta a partir do lançamento do álbum (vitórias e pontos antes dele não geram pacote).
- Reconciliação: `BackgroundService` a cada ~5 minutos, no padrão do `NotificacoesService`
  (`IServiceScopeFactory`, escopo por execução, erro isolado).
- Aviso no celular quando ganha pacote ("🎴 Você ganhou 1 pacote pela vitória sobre o Grêmio"),
  com chave de deduplicação em `NotificacoesEnviadas`. Hoje não existe "push para a pessoa X"
  público no `PushService`; criar seguindo o `LembrarBolaoAsync`.

## Telas

- **/album** (menu Competições ou um grupo novo "Álbum"; atalho na barra inferior a avaliar):
  - capa com progresso (x de y, % e por raridade), pacotes para abrir e o botão do pacote do dia;
  - índice de páginas (escudo de cada clube com % completo e selo quando completa);
  - página do clube: grade de figurinhas; vaga vazia mostra o número e o nome em cinza (como o
    álbum de verdade), figurinha colada mostra a arte;
  - filtro "só as que faltam" e busca por jogador.
- **Abrir pacote**: animação do pacote rasgando e das cartas virando uma a uma; brilhante e
  lendária com efeito especial; botão de compartilhar a melhor carta (`ShareableCard`).
- **Repetidas e trocas** (fase 4): monte de repetidas, propostas de troca, sugestões automáticas.
- **Admin**: lançar/gerar o álbum da temporada, ver figurinhas sem foto (com link para trocar a
  foto do jogador), dar pacotes, ajustar X do bolão.
- **Minha Área**: cartão "Álbum" com progresso e pacotes para abrir.
- **Hall da Fama**: seção Colecionadores. Não usar `TipoCompetition` para isso (é o tipo de liga);
  ler das conquistas do álbum.

## A figurinha

Cartão vertical no estilo Panini/FUT, feito em HTML/CSS (escala bem e não depende de imagem pronta):
foto do jogador (`FotoJogador`, `Lazy`), número, nome, posição (sigla), overall, escudo do clube,
faixa com a cor da raridade. Brilhante e lendária com gradiente holográfico que mexe com o mouse
(e com o giroscópio no celular, se der). Sem foto: silhueta `jogador-sem-foto.svg` e o card continua
bonito. Precisa ficar bom no tema claro e no escuro.

## Modelo de dados (primeira versão)

- `Album(AlbumId, Nome, Temporada, LancadoEm, Ativo)`
- `Figurinha(FigurinhaId, AlbumId, Numero, Tipo [Jogador|Escudo], Raridade [Comum|Brilhante|Lendaria],
  TeamId, PlayerId?, NomeImpresso, PosicaoSigla?, Overall?, Destaque?, Ordem)` — retrato do momento do
  lançamento; `Destaque` é o texto da lendária escrito pelo admin.
- `FigurinhaDoTreinador(TreinadorId, FigurinhaId, Quantidade, PrimeiraEm)` — `Quantidade - 1` = repetidas.
- `PacoteGanho(Id, TreinadorId, AlbumId?, Origem, Chave, CriadoEm, AbertoEm?)`
- `PacoteAberto(PacoteId, TreinadorId, AbertoEm, FigurinhasJson)` — histórico e feed (ou colunas no próprio `PacoteGanho`).
- `AlbumConquista(TreinadorId, AlbumId, Tipo [PaginaCompleta|AlbumCompleto], TeamId?, Em)` — selos e Hall da Fama.
- Fase 4: `TrocaFigurinhas(TrocaId, DeTreinadorId, ParaTreinadorId, Status, CriadaEm, RespondidaEm)` +
  itens oferecidos/pedidos.

Convenções do projeto: entidade em `Fc25Draft.Core/Entities`, configuração em
`Fc25Draft.Infra/Configurations` (aplicada por `ApplyConfigurationsFromAssembly`), `DbSet` no
`DraftDbContext`, serviço de página com `AddComConexaoPropria`, migração com
`dotnet ef migrations add Nome --project src/Fc25Draft.Infra --startup-project src/Fc25Draft.Web`
(o app aplica sozinho ao subir). Pessoa logada: `AdminAuth.GetTokenAsync()` +
`ITreinadorService.GetPorTokenAsync`. Página admin: `@attribute [Authorize(Roles = "Admin")]`.
Menu: `LayoutNavigationService.MenuDefinition`. Textos e nomes em português, como o resto do código.

## Fases

### Fase 1 — Álbum de pé
Entidades e migração; geração do álbum da temporada pelo admin (com prévia: quantos clubes,
figurinhas por raridade, quantas sem foto, e a escolha das 10 lendárias com sugestões pelas notas
da temporada passada); componente da figurinha; página /album com capa,
índice e páginas dos clubes; admin dá pacotes; abrir pacote (sem animação ainda) com sorteio por
raridade; tela admin de figurinhas sem foto.
**Pronto quando:** o admin gera o álbum no banco local, dá 3 pacotes a um treinador de teste, ele
abre, as figurinhas aparecem coladas e as repetidas contadas.

**Feito (03/10/2026).** Como ficou, para as próximas fases:
- Entidades em `Fc25Draft.Core/Entities/Album.cs` (`Album`, `Figurinha`, `FigurinhaDoTreinador`,
  `PacoteGanho`); migração `AlbumDeFigurinhas`. O `PacoteGanho` guarda as figurinhas que saíram
  (`Figurinhas`, `uuid[]`) e um `Motivo` em texto, em vez de uma tabela `PacoteAberto`.
  `FigurinhaDoTreinador.Nova` marca a colada que a pessoa ainda não viu na página do clube.
- Regras e números em `Core/Utilities/AlbumFigurinhas.cs` (montagem, sorteio, constantes);
  serviço `IAlbumService`/`AlbumService`. Abrir pacote trava a linha do treinador (`FOR UPDATE`)
  para dois cliques não abrirem o mesmo pacote.
- Telas: `/album` (página do clube em `/album?clube={teamId}`), `/admin/album` (prévia, lançar e
  lendárias), `/admin/album/pacotes`, `/admin/album/sem-foto`. Componente `Components/Album/CartaFigurinha`.
  Menu: grupo "Álbum" e "Admin · Álbum". Log de ações: `LancarAlbum`, `DarPacotes`, `AjustarLendarias`.
- Lançar outro álbum desativa o anterior. As lendárias podem ser trocadas no lugar enquanto nenhum
  pacote do álbum foi aberto (quem sai volta a brilhante ou comum pela regra do clube).

### Fase 2 — Ganhar pacotes
Livro-razão `PacoteGanho`; pacote do dia; reconciliação em segundo plano de vitórias e bolão;
aviso no celular; cartão na Minha Área; ajuste do X do bolão no admin.
**Pronto quando:** encerrar um jogo no banco local dá pacote aos dois do clube vencedor, rodar a
reconciliação duas vezes não duplica, e o pacote do dia só sai uma vez por dia.

**Feito (03/10/2026).** Como ficou, para as próximas fases:
- Chaves e textos em `AlbumFigurinhas` (`ChaveDiario`, `ChaveVitoria`, `ChaveBolao`, `DeOndeVeio`,
  `AvisoDePacotes`). Pacote do dia: `IAlbumService.PegarPacoteDoDiaAsync`, data de Brasília
  (`HorarioDeBrasilia`, o equivalente do `BrazilTime` no Core/Infra); clique duplo cai no índice único.
- Reconciliação: `IAlbumService.ReconciliarAsync`, chamada pelo `PacotesDoAlbumService` (Web/Services,
  a cada 5 min) e pelo botão "Conferir vitórias e bolão agora" em `/admin/album/pacotes`. Conta só
  partidas `Encerrada` com `EncerradaEm >= LancadoEm` do álbum ativo. Vencedor pelo placar ou pelos
  pênaltis; empate não dá pacote; W.O. entra pelo placar de 2x0. Dia do jogo = `DataHora` da rodada
  (Brasília) ou, sem data, o dia do encerramento. Bolão: só jogos da temporada do álbum encerrados
  depois do lançamento, mesma conta do `BolaoPontuacao` (sem pênaltis).
- X do bolão: coluna `Album.PontosBolaoPorPacote` (padrão 30), editada em `/admin/album` e registrada
  no log (`AjustarPontosBolao`). O projeto não tem tabela genérica de configurações (cada assunto tem a
  sua linha única, como `TransferConfig`), e o X é por álbum/temporada, então ficou no próprio álbum.
  Mudar o X nunca tira pacote; baixar dá os que passaram a ser devidos.
- Aviso no celular: `IPushService.AvisarPacotesGanhosAsync`, uma marca `pacote:{id}` por pacote em
  `NotificacoesEnviadas`, um aviso por pessoa juntando os novos. Não avisa o pacote do dia (a pessoa
  pegou no site), pacote já aberto nem pacote com mais de 1 dia.
- Telas: botão do pacote do dia e lista "Fechados" (de onde veio cada um) na capa do `/album`;
  cartão `Components/Album/AlbumCartao` na Minha Área.

### Fase 3 — Experiência
Animação de abrir pacote, holográfico, compartilhar a carta, feed "últimas raras tiradas",
ranking de colecionadores, selos de página completa, Hall da Fama (Colecionadores).

### Fase 4 — Trocas
Monte de repetidas; proposta de troca entre treinadores (aceite atômico, sem perder figurinha em
corrida); sugestões automáticas ("Fulano tem 3 que você precisa e precisa de 2 suas"); reciclar
repetidas (ex.: 6 repetidas → 1 pacote).

## Ideias para depois

Páginas especiais (lendas do Hall da Fama, treinadores, momentos da temporada da linha do tempo),
figurinha de edição limitada do craque da rodada (só sai nos pacotes daquela semana), torcedores
colecionando também, álbum físico em PDF para imprimir.
