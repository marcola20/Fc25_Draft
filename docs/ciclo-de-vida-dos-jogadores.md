# Ciclo de vida dos jogadores

Plano geral de como os jogadores nascem, crescem, envelhecem e se aposentam na CBFV. Trabalho na branch
`feat/categorias-de-base`. Revisto em 07/10/2026 com as decisões e a simulação abaixo; as categorias de base
(`docs/categorias-de-base.md`) viraram a "nova geração" deste plano.

## A ideia em uma frase

Uma vez por temporada, na virada, **todo jogador envelhece** (no site e no save do PES) e o overall dele **muda
pela idade e pelo quanto jogou** — os jovens sobem, os do auge oscilam, os veteranos caem e os mais velhos se
aposentam com aviso antecipado. Entra uma **nova geração** de jovens, que chega aos clubes pelo **draft sub-23**.
Tudo passa por uma **prévia que o admin aprova**, e o site e o Editor PES ficam sempre iguais.

## Decisões

| Tema | Decisão |
|---|---|
| Quem muda | **Todos os jogadores**, pela curva de idade + desempenho. |
| Curva | A "curva G" da simulação (abaixo). A do plano anterior inflava a liga. |
| Teto | **Indefinido**: ninguém tem limite fixo; quanto cresce depende de quanto e como joga. Só o limite técnico do jogo (94 por enquanto, ver "A decidir"). |
| Draft | **O draft passa a ser só de jovens**: cada draft tem, além da faixa de overall que já existe, uma **idade máxima** (23). Substitui o draft atual por completo; veteranos livres chegam aos clubes pelo leilão. Substitui também o "draft da base aos 17" de `categorias-de-base.md`. |
| Nova geração | O admin adiciona jovens a partir da base do PES (ou cria jogadores que nem estão no jogo), **com overall de 80 para cima**, ajustando o overall na entrada. Os jovens livres que já estão no site ficam fora do leilão para abastecer o próximo draft. Sem quantidade fixa por temporada. |
| Idade | **O site manda.** "Envelhecer todos (+1)" uma vez por temporada, na virada; o Editor PES copia a idade do site para o save (espelho, não "+1"). |
| Aposentadoria | **Automática com aviso**: o site anuncia quem está na "última temporada" e ele se aposenta na virada seguinte. O admin pode segurar ou antecipar. |
| Quando | **Uma vez por temporada, na virada**, com prévia para aprovar. |

## O que os dados mostraram (banco local, 06/10/2026)

- 486 jogadores, 297 em times e 189 livres; todos com idade e atributos do PES.
- A liga é **apertada**: quase todos entre 82 e 88; média dos 300 melhores = **85,8**; 41 com 88+, 10 com 90+. No preço,
  cada ponto de overall vale ~10% (`PricingConfig.OverallBase` = 1,10); a idade já pesa no preço (`AgeFactor`, 1,18 → 0,85).
- O elenco é **velho**: 27 jogadores com 36+ (18 em times) e 45 com 33–35. A aposentadoria mexe nos clubes.
- **Jovens**: 59 livres com até 23 anos no site. A base do PES embutida (`pes-jogadores.json.gz`, 23.744 jogadores) é uma
  foto de 2009/2010, o ano da liga: tem os craques que surgiram depois ainda garotos (Coutinho 17 anos/86, Sterling 15/81,
  Götze 16/76, Pogba 15/62) e centenas de jovens com 80+ em cada idade. As idades do site batem com essa foto
  (Neymar 17): as idades de hoje valem como **idade na temporada 2010**.
- O Editor PES já grava idade no save (6 bits) e no banco do patch (5 bits, +15) — ver `D:\PES\EditorPES\editor\formato.py`.

## Simulação (5 temporadas, jogadores reais; desempenho sorteado em ±1, aposentadoria pela tabela abaixo)

| Cenário (em 2015) | Média top 300 | 90+ | 88+ |
|---|---|---|---|
| Hoje | 85,8 | 10 | 41 |
| Curva do plano anterior, sem teto | 88,7 | 110 | 177 |
| **Curva G, sem teto, 25 novos/temporada (80–84)** | **85,3** | **~22** | **~60** |
| Curva G, sem teto, só evolui quem joga | 84,2 | ~10 | ~28 |

Conclusão: com a curva G a média fica estável e surgem estrelas novas aos poucos; o desempenho real (titular, nota)
fica entre os dois últimos cenários. Os números finais se calibram na prévia com os jogos reais.

## As peças

### 1. Idade que anda (site e editor)
- **Virada**: passo "Envelhecer todos (+1)" em `/admin/temporada`, que só roda **uma vez por temporada** (registro por
  temporada; tentar de novo é recusado) e mostra antes quantos jogadores e quem passa de faixa de preço.
- **Editor PES**: botão "Sincronizar idades com o site" — baixa a idade de cada jogador ligado (endpoint novo, ex.
  `GET /api/admin/pes/idades`) e grava no save e no banco do patch **a idade do site**. É espelho: pode rodar quantas vezes
  quiser sem somar duas vezes. Entra também na sincronização normal de quando o editor abre.
- **Jogador novo vindo da base do PES**: idade na liga = idade na base + (temporada − 2010).

### 2. A curva de idade ("curva G", variação base por temporada)

| Idade na temporada | até 20 | 21–23 | 24–27 | 28–29 | 30–31 | 32–33 | 34+ |
|---|---|---|---|---|---|---|---|
| Variação | +2 | +1 | 0 | −1 | −2 | −3 | −4 |

Goleiro envelhece mais devagar: a tabela dele anda 3 anos (um goleiro de 33 usa a linha de 30).

### 3. Desempenho (para todos)
Pela temporada que acabou, com os dados que o site já tem (titulares em `LigaEscalacoesPartida`, entradas em
`LigaEventos`, notas em `LigaNotasJogadores`):

| Situação na temporada | Efeito |
|---|---|
| Titular em mais de 60% dos jogos do clube | +1 |
| Nota média do PES ≥ 7,0 | +1 |
| Nota média do PES < 6,0 (com pelo menos 5 jogos) | −1 |
| Titular em menos de 20% dos jogos (ou sem clube) | −1 |

Somado à curva. Um veterano de 32 (−3) titular absoluto com nota 7,2 fica em −1: segura quase todo o nível. Um jovem de
20 (+2) que não joga fica em +1.

### 4. Fora da curva
- **Surpresas** (sorteadas na prévia): pequena chance de **explosão** (+2 a +4 a mais; mais comum até 25 anos) ou de
  **queda de rendimento** (−2 a −4; mais comum a partir de 29). Algo como 5% cada. Destacadas na prévia; o admin desfaz.
- **Ajuste manual**: o admin sobe ou desce qualquer jogador a qualquer momento com um motivo. Fica registrado na ficha.

### 5. De overall para atributos
- **Subida**: `OverallPes.Evoluir` (já existe, usado na venda rápida).
- **Queda**: função nova no Core, o inverso — com a idade caem primeiro os **físicos** (velocidade, aceleração,
  resistência, impulsão); técnicos e mentais caem pouco; goleiro perde reflexo e agilidade por último.
- Cada mudança vira uma `EvolucaoPes` pendente com o motivo ("Temporada 2011: +2 — idade 22, titular, nota 7,1"), que o
  Editor PES aplica no save pelo caminho que já existe. O gráfico da ficha (`EvolucaoOverall`) já mostra `EvolucaoPes`.

### 6. Nova geração e draft sub-23
- **Draft**: o draft ganha **idade máxima** (configurável por draft, padrão 23), ao lado da faixa de overall das
  rodadas. O pote é "livre e com até N anos na temporada". Ordem e ferramentas do draft continuam as mesmas.
- **Jovens reservados**: jogador livre com até a idade máxima do próximo draft **não entra no leilão**
  (`MarketItemGenerationService`, `MarketCycleGenerator`); veterano livre vai para o leilão.
- **Nova geração**: tela do admin para trazer jovens da base do PES (sugere os melhores que ainda não estão no site, já
  com a idade da liga), definir o overall de entrada (80+) e criar jogadores que não existem no jogo. O ajuste do overall
  vai para o save como `EvolucaoPes` (ou o jogador é criado no editor e ligado pelo `PesId`).
- **Próxima temporada**: os 59 jovens livres que já estão no site bastam; o admin só não os coloca no próximo leilão.

### 7. Aposentadoria
- **Anúncio**: na virada, depois das mudanças, o site escolhe quem entra na **última temporada**: chance pela idade
  (34 → 10%, 35 → 25%, 36 → 50%, 37 → 75%, 38+ → sempre), menor para quem ainda tem overall alto. Vai na prévia; o admin
  confirma, tira ou acrescenta. **Na primeira virada ninguém se aposenta de surpresa**: só há anúncios.
- **Durante a temporada**: selo "Última temporada" na ficha, no elenco e na figurinha; notícia de despedida.
- **Na virada seguinte**: sai do elenco e fica marcado como aposentado, **continuando no histórico** (carreira, recordes,
  Hall da Fama, álbum). Se o clube cair abaixo do mínimo de elenco vale a regra que já existe (aviso e só contrata).
- **Editor PES**: tira os aposentados dos elencos dos clubes no save.

### 8. A virada da temporada
Passo novo em `/admin/temporada`: **"Ciclo de vida da temporada"** — envelhecer, evolução/queda, aposentadorias e
anúncios, com **prévia** por jogador (idade, jogos, titular %, nota, curva, desempenho, surpresa, ajuste, variação final,
overall antes → depois) e **confirmar uma vez só** por temporada. Gera as `EvolucaoPes`, a lista de idades e de
aposentados para o editor, e os avisos aos treinadores.

### 9. Telas
- **Ficha do jogador**: idade, variação da última virada com a conta explicada, previsão da próxima, selos.
- **Página do clube**: idade média do elenco, quem está em fim de carreira.
- **/virada** (pública): quem mais subiu e caiu, explosões, aposentadorias e despedidas, com cards para compartilhar.
- **Hall da Fama**: "Lendas aposentadas".

## Ordem

1. **Idade que anda** — "Envelhecer todos (+1)" com trava por temporada no site; endpoint de idades; "Sincronizar
   idades" no Editor PES. (Pedido primeiro; pequeno e independente.)
2. **Draft sub-23** — idade máxima no draft; jovens livres fora do leilão; tela da nova geração.
3. **Evolução da virada** — curva G, desempenho, atributos (subida e queda), prévia e confirmação, `EvolucaoPes`.
4. **Aposentadoria** — anúncio, última temporada, saída na virada, editor tirando dos elencos, Lendas aposentadas.
5. **Fora da curva e experiência** — surpresas, ajuste manual, página /virada, avisos no celular.

Cada passo numa sessão, anotando "Feito" aqui no fim.

## A decidir (perguntar antes do passo em que pesa)

- Limite técnico de overall (94?) para explosões seguidas. Passo 5.
- Venda rápida continua evoluindo do mesmo jeito, somando com a virada? Passo 3.
- Aposentado vira figurinha especial de "Lenda" no álbum seguinte? Passo 4.
- O que acontece com quem tem mais de 23 e ainda está livre e sem lance no leilão por muito tempo? Passo 2.
