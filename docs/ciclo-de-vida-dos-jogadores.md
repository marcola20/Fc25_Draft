# Ciclo de vida dos jogadores

Plano geral de como os jogadores nascem, crescem, envelhecem e se aposentam na CBFV. As categorias de base
(`docs/categorias-de-base.md`) são uma parte dele: a entrada dos jovens. Trabalho na branch
`feat/categorias-de-base`.

## A ideia em uma frase

Uma vez por temporada, na virada, **todo jogador envelhece** e o overall dele **muda pela idade, pelo quanto
jogou e por surpresas** — os jovens sobem, os do auge oscilam, os veteranos caem e os mais velhos se aposentam
com aviso antecipado. Tudo passa por uma **prévia que o admin aprova**, e as mudanças vão para o save do PES pelo
caminho que já existe (`EvolucaoPes` → Editor PES).

## Decisões já tomadas

| Tema | Decisão |
|---|---|
| Quem muda | **Todos os jogadores**, pela curva de idade. As promessas da base seguem a tabela própria delas rumo ao teto. |
| Desempenho | **Conta para todos**: titular com boa nota sobe mais (ou cai menos); quem fica no banco sobe menos (ou cai mais). |
| Além da curva | **Surpresas** (explosão e queda de rendimento) e **ajuste manual** do admin com motivo. |
| Aposentadoria | **Automática com aviso**: no começo da temporada o site marca quem está na "última temporada"; na virada seguinte ele se aposenta. O admin pode segurar ou antecipar. |
| Quando | **Uma vez por temporada, na virada**, com prévia para aprovar. |
| Base | Ver `docs/categorias-de-base.md`: cadastro quando quiser, idade definida pelo admin (até 17), draft da base aos 17. |

## As peças

### 1. Idade para todos
- Todo jogador passa a ter **ano de nascimento na liga** (temporada atual − idade atual, calculado uma vez a
  partir do `Player.Age` de hoje). A idade de cada temporada sai dele: ninguém precisa mais "somar 1".
- Jogador sem idade aparece numa lista no admin para completar antes da primeira virada.

### 2. A curva de idade (variação base por temporada, valores iniciais ajustáveis)

| Idade na temporada | Variação base |
|---|---|
| até 20 | +3 |
| 21–23 | +2 |
| 24–26 | +1 |
| 27–29 | 0 |
| 30–31 | −1 |
| 32–33 | −2 |
| 34–35 | −3 |
| 36+ | −4 |

Goleiro envelhece mais devagar: a tabela dele anda 3 anos (um goleiro de 33 usa a linha de 30). Promessa da base
usa a tabela da base até se formar e depois esta.

### 3. Desempenho (para todos)
Pela temporada que acabou, com os dados que o site já tem (titulares em `LigaEscalacoesPartida`, entradas em
`LigaEventos`, notas em `LigaNotasJogadores`):

| Situação na temporada | Efeito |
|---|---|
| Titular em mais de 60% dos jogos do clube | +1 |
| Nota média do PES ≥ 7,0 | +1 |
| Nota média do PES < 6,0 (com pelo menos 5 jogos) | −1 |
| Titular em menos de 20% dos jogos | −1 |

Somado à curva. Um veterano de 32 (−2) titular absoluto com nota 7,2 fica em 0: segura o nível.

### 4. Fora da curva
- **Surpresas** (sorteadas na prévia, valores iniciais): cada jogador tem uma pequena chance de **explosão**
  (+2 a +4 a mais; mais comum até 25 anos) ou de **queda de rendimento** (−2 a −4; mais comum a partir de 29).
  Algo como 5% cada, para ser raro e virar notícia. Aparecem destacadas na prévia e o admin pode desfazer uma.
- **Ajuste manual**: o admin sobe ou desce qualquer jogador a qualquer momento com um motivo (lesão grave,
  fase iluminada, decisão da liga). Fica registrado e aparece na ficha.

### 5. De overall para atributos
- **Subida**: `OverallPes.Evoluir` (já existe, usado na venda rápida).
- **Queda**: função nova no Core, o inverso — com a idade caem primeiro os **físicos** (velocidade, aceleração,
  resistência, impulsão), os técnicos e mentais caem pouco; goleiro perde reflexo e agilidade por último.
- Cada mudança vira uma `EvolucaoPes` pendente com o motivo ("Temporada 2010: +2 — idade 22, titular, nota
  7,1"), que o Editor PES aplica no save. O gráfico da ficha (`EvolucaoOverall`) já mostra `EvolucaoPes`.
- Jogador sem atributos do PES muda o overall direto (como a venda rápida faz hoje).

### 6. Aposentadoria
- **Anúncio**: na virada, depois das mudanças, o site escolhe quem entra na **última temporada**: chance pela
  idade (valores iniciais: 34 → 10%, 35 → 25%, 36 → 50%, 37 → 75%, 38+ → sempre), menor para quem ainda tem
  overall alto. A lista vai na prévia; o admin confirma, tira ou acrescenta.
- **Durante a temporada**: selo "Última temporada" na ficha, no elenco e na figurinha; notícia de despedida.
- **Na virada seguinte**: o jogador se aposenta — sai do elenco (`TeamRoster` e `CurrentTeamId`), fica marcado
  como aposentado e **continua no histórico** (carreira, recordes, Hall da Fama, álbum). O clube não recebe
  nada; se cair abaixo do mínimo de elenco vale a regra que já existe (aviso e só contrata).
- O admin recebe a lista dos aposentados para tirar do save no Editor PES.

### 7. A virada da temporada
Um passo novo em `/admin/temporada`, depois de gerar a temporada: **"Ciclo de vida da temporada"**.
- **Prévia** por jogador: idade, jogos, titular %, nota média, curva, desempenho, surpresa, ajuste, variação
  final, overall antes → depois, e a lista de aposentadorias e anúncios. Filtros por clube e ordenação.
- **Confirmar** aplica tudo de uma vez, uma vez só por temporada (registro `CicloDaTemporada` por temporada e
  `VariacaoDaTemporada` por jogador com a conta), cria as `EvolucaoPes` e avisa os treinadores no celular
  ("Götze subiu +4: agora 79"; "Nesta anunciou a última temporada").

### 8. Telas
- **Ficha do jogador**: idade, variação da última virada com a conta explicada, previsão da próxima ("jogando
  assim, deve ficar entre −1 e +1"), selo de última temporada / aposentado / cria da base.
- **Página do clube**: idade média do elenco, quem está em fim de carreira, crias da base.
- **/virada** (pública): a virada vira notícia — quem mais subiu, quem mais caiu, explosões, aposentadorias e
  anúncios de despedida, com cards para compartilhar.
- **Hall da Fama**: "Lendas aposentadas" com a carreira de cada um.

## Ordem sugerida

1. **Idade para todos** — ano de nascimento na liga, lista de quem está sem idade. Base de tudo.
2. **Base, fases 1 e 2** (`docs/categorias-de-base.md`) — cadastro das promessas e draft da base, para a próxima
   temporada já ter os garotos.
3. **Evolução da virada** — curva, desempenho, atributos (subida e queda), prévia e confirmação, `EvolucaoPes`;
   inclui a evolução das promessas (base, fase 3).
4. **Aposentadoria** — anúncio, última temporada, saída na virada, Lendas aposentadas.
5. **Fora da curva e experiência** — surpresas, ajuste manual, página /virada, avisos no celular, base fase 4.

Cada passo numa sessão nova, como no álbum, anotando "Feito" aqui no fim.

## A decidir (perguntar antes do passo em que pesa)

- Os números das tabelas (curva, desempenho, surpresas, chances de aposentadoria): os de cima são o ponto de
  partida; vale simular com o elenco real na prévia antes de valer. Passo 3.
- Limite de overall: um teto geral (ex.: 94) para ninguém disparar com explosões seguidas? Passo 5.
- Venda rápida continua evoluindo do mesmo jeito, somando com a virada? Passo 3.
- Aposentado vira figurinha especial de "Lenda" no álbum seguinte? Passo 4.
- Idade no save do PES: o `EvolucaoPes` leva só atributos; a idade o admin ajusta no editor ou o editor passa a
  ler a idade do site. Passo 1.
