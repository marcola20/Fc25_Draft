using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Extensions;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Regras do álbum de figurinhas: como as páginas são montadas a partir dos elencos e como o pacotinho
/// é sorteado. Os números ficam aqui para ajustar num lugar só.
/// </summary>
public static class AlbumFigurinhas
{
    /// <summary>Figurinhas por pacote.</summary>
    public const int TamanhoPacote = 5;

    /// <summary>Chance de uma das vagas comuns (1 a 4) vir brilhante.</summary>
    public const double ChanceBrilhanteNaComum = 0.05;

    /// <summary>Chance da última vaga (a especial) vir lendária em vez de brilhante.</summary>
    public const double ChanceLendariaNaEspecial = 0.15;

    /// <summary>Maiores overalls de cada clube que viram brilhantes (além do escudo).</summary>
    public const int BrilhantesPorClube = 3;

    public const int MaximoLendarias = 10;

    /// <summary>Jogos com nota na temporada passada para entrar nas sugestões de lendária.</summary>
    public const int MinimoJogosParaSugestao = 3;

    public const int TamanhoDestaque = 80;

    /// <summary>Técnico ou auxiliar com passagem aberta no clube no lançamento.</summary>
    public sealed record TecnicoDoAlbum(Guid TreinadorId, string Nome, PapelTreinador Papel);

    public sealed record ClubeDoAlbum(Guid TeamId, string Nome, IReadOnlyList<TecnicoDoAlbum>? Tecnicos = null);

    public sealed record JogadorDoAlbum(int PlayerId, string Nome, int PositionId, int Overall, Guid TeamId);

    /// <summary>Uma figurinha montada, antes de virar entidade.</summary>
    public sealed record FigurinhaMontada(
        int Numero,
        TipoFigurinha Tipo,
        RaridadeFigurinha Raridade,
        Guid TeamId,
        int? PlayerId,
        string NomeImpresso,
        string? PosicaoSigla,
        int? Overall,
        string? Destaque,
        int Ordem,
        Guid? TreinadorId = null,
        PapelTreinador? Papel = null);

    /// <summary>
    /// Monta o álbum: uma página por clube, na ordem recebida, com o escudo, o técnico e o auxiliar (quem
    /// tem passagem aberta) e depois o elenco por posição (GOL, ZAG, LE… na ordem do <c>PositionType</c>) e,
    /// dentro da posição, por overall. Numeração corrida. A figurinha de técnico é sorteada como brilhante.
    /// </summary>
    public static IReadOnlyList<FigurinhaMontada> Montar(
        IReadOnlyList<ClubeDoAlbum> clubes,
        IReadOnlyList<JogadorDoAlbum> jogadores,
        IReadOnlyDictionary<int, string?> lendarias)
    {
        var resultado = new List<FigurinhaMontada>();
        var numero = 0;
        var porClube = jogadores.ToLookup(j => j.TeamId);

        foreach (var clube in clubes)
        {
            resultado.Add(new FigurinhaMontada(++numero, TipoFigurinha.Escudo, RaridadeFigurinha.Brilhante,
                clube.TeamId, null, clube.Nome, null, null, null, 0));

            var ordem = 0;
            foreach (var t in (clube.Tecnicos ?? Array.Empty<TecnicoDoAlbum>()).OrderBy(t => t.Papel))
            {
                resultado.Add(new FigurinhaMontada(++numero, TipoFigurinha.Treinador, RaridadeFigurinha.Brilhante,
                    clube.TeamId, null, t.Nome, t.Papel == PapelTreinador.Auxiliar ? "AUX" : "TÉC", null, null, ++ordem,
                    t.TreinadorId, t.Papel));
            }

            var elenco = porClube[clube.TeamId].ToList();
            var brilhantes = MaioresOveralls(elenco.Select(j => (j.PlayerId, j.Overall, j.Nome)));

            foreach (var j in elenco.OrderBy(j => j.PositionId).ThenByDescending(j => j.Overall).ThenBy(j => j.Nome, StringComparer.CurrentCulture))
            {
                var lendaria = lendarias.TryGetValue(j.PlayerId, out var destaque);
                var raridade = lendaria ? RaridadeFigurinha.Lendaria
                    : brilhantes.Contains(j.PlayerId) ? RaridadeFigurinha.Brilhante
                    : RaridadeFigurinha.Comum;

                resultado.Add(new FigurinhaMontada(++numero, TipoFigurinha.Jogador, raridade, clube.TeamId, j.PlayerId,
                    j.Nome, j.PositionId.ToPositionSigla(), j.Overall, lendaria ? Limpar(destaque) : null, ++ordem));
            }
        }

        return resultado;
    }

    /// <summary>Os <see cref="BrilhantesPorClube"/> maiores overalls do elenco (empate pelo nome).</summary>
    public static HashSet<int> MaioresOveralls(IEnumerable<(int PlayerId, int Overall, string Nome)> elenco) =>
        elenco.OrderByDescending(j => j.Overall).ThenBy(j => j.Nome, StringComparer.CurrentCulture)
            .Take(BrilhantesPorClube)
            .Select(j => j.PlayerId)
            .ToHashSet();

    public static string? Limpar(string? destaque)
    {
        var texto = destaque?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        return texto.Length > TamanhoDestaque ? texto[..TamanhoDestaque] : texto;
    }

    /// <summary>
    /// Sorteia um pacote. Vagas 1 a 4: comum, com <see cref="ChanceBrilhanteNaComum"/> de virar brilhante;
    /// a última: brilhante, com <see cref="ChanceLendariaNaEspecial"/> de virar lendária. Sem figurinha da
    /// raridade sorteada no álbum, desce para a de baixo. Não repete figurinha dentro do mesmo pacote
    /// enquanto houver outra da raridade.
    /// </summary>
    public static IReadOnlyList<Guid> SortearPacote(
        IReadOnlyDictionary<RaridadeFigurinha, IReadOnlyList<Guid>> porRaridade, Random rng)
    {
        var sorteadas = new List<Guid>(TamanhoPacote);
        for (var vaga = 1; vaga <= TamanhoPacote; vaga++)
        {
            var especial = vaga == TamanhoPacote;
            var raridade = especial
                ? rng.NextDouble() < ChanceLendariaNaEspecial ? RaridadeFigurinha.Lendaria : RaridadeFigurinha.Brilhante
                : rng.NextDouble() < ChanceBrilhanteNaComum ? RaridadeFigurinha.Brilhante : RaridadeFigurinha.Comum;

            for (var r = raridade; r >= RaridadeFigurinha.Comum; r--)
            {
                if (!porRaridade.TryGetValue(r, out var opcoes) || opcoes.Count == 0) continue;

                var livres = opcoes.Where(id => !sorteadas.Contains(id)).ToList();
                var lista = livres.Count > 0 ? livres : opcoes;
                sorteadas.Add(lista[rng.Next(lista.Count)]);
                break;
            }
        }

        return sorteadas;
    }

    /// <summary>"#007": o número como vem impresso.</summary>
    public static string NumeroImpresso(int numero) => $"#{numero:000}";

    // ---- Pacotes ganhos ----

    /// <summary>X inicial do bolão: 1 pacote a cada X pontos (≈ 3 placares cravados ou 6 resultados certos).</summary>
    public const int PontosBolaoPorPacotePadrao = 30;

    /// <summary>Pacotes que o botão do pacote do dia dá, uma vez por pessoa por data de Brasília.</summary>
    public const int PacotesDoDia = 2;

    /// <summary>Pacotes por jogo do seu time: vitória, empate e derrota (quem faz W.O. não ganha nada).</summary>
    public const int PacotesPorVitoria = 3;
    public const int PacotesPorEmpate = 2;
    public const int PacotesPorDerrota = 1;

    /// <summary>
    /// O n-ésimo pacote do dia (1, 2…). O primeiro mantém a chave de quando era um só, então quem já pegou
    /// hoje antes da mudança não pega de novo.
    /// </summary>
    public static string ChaveDiario(DateTime diaEmBrasilia, int n = 1) =>
        n == 1 ? $"diario:{diaEmBrasilia:yyyy-MM-dd}" : $"diario:{diaEmBrasilia:yyyy-MM-dd}:{n}";

    /// <summary>O n-ésimo pacote (1, 2, 3) de um jogo do time da pessoa.</summary>
    public static string ChaveJogo(Guid partidaId, int n) => $"jogo:{partidaId:N}:{n}";

    /// <summary>Todo mundo (com clube ou sem) ganha 1 pacote em cada dia com jogo (data de Brasília).</summary>
    public static string ChaveDiaDeJogo(DateTime diaEmBrasilia) => $"diadejogo:{diaEmBrasilia:yyyy-MM-dd}";

    public static string MotivoDiaDeJogo(DateTime diaEmBrasilia) => $"Dia de jogo · {diaEmBrasilia:dd/MM}";

    /// <summary>O n-ésimo pacote do bolão da temporada (1, 2, 3…).</summary>
    public static string ChaveBolao(int temporada, int n) => $"bolao:{temporada}:{n}";

    /// <summary>Quantos pacotes do bolão os pontos já valem.</summary>
    public static int PacotesDoBolao(int pontos, int pontosPorPacote) =>
        pontosPorPacote <= 0 || pontos <= 0 ? 0 : pontos / pontosPorPacote;

    public static string MotivoVitoria(string adversario) => $"Vitória sobre o {adversario}";
    public static string MotivoEmpate(string adversario) => $"Empate com o {adversario}";
    public static string MotivoDerrota(string adversario) => $"Derrota para o {adversario}";

    /// <summary>"pela vitória sobre o Grêmio", "pelo empate com o Grêmio"…</summary>
    private static string PeloJogo(string motivo) =>
        $"{(motivo.StartsWith("Empate", StringComparison.Ordinal) ? "pelo" : "pela")} {Minuscula(motivo)}";

    // ---- Trocas e reciclagem ----

    /// <summary>Repetidas que viram 1 pacote novo na reciclagem.</summary>
    public const int RepetidasPorPacote = 6;

    /// <summary>Prazo para responder uma proposta de troca.</summary>
    public static readonly TimeSpan PrazoDaTroca = TimeSpan.FromHours(48);

    /// <summary>Figurinhas no máximo de cada lado de uma proposta.</summary>
    public const int MaximoPorLadoDaTroca = 10;

    public static string ChaveReciclagem(Guid pacoteId) => $"reciclagem:{pacoteId:N}";

    public static string MotivoReciclagem => $"{RepetidasPorPacote} repetidas recicladas";

    /// <summary>"2 por 1": quantas a pessoa dá e quantas recebe, do ponto de vista de quem propôs.</summary>
    public static string Placar(int oferecidas, int pedidas) => $"{oferecidas} por {pedidas}";

    public static string MotivoBolao(int pontos, int temporada) => $"{pontos} pontos no bolão de {temporada}";

    /// <summary>De onde veio o pacote, em poucas palavras, para a lista dos fechados.</summary>
    public static string DeOndeVeio(string origem, string? motivo) => origem switch
    {
        PacoteGanho.OrigemDiario => "Pacote do dia",
        PacoteGanho.OrigemJogo => motivo ?? "Jogo do seu time",
        PacoteGanho.OrigemDiaDeJogo => motivo ?? "Dia de jogo",
        PacoteGanho.OrigemBolao => motivo is null ? "Bolão" : $"Bolão · {motivo}",
        PacoteGanho.OrigemAdmin => motivo is null ? "Da organização" : $"Da organização · {motivo}",
        PacoteGanho.OrigemReciclagem => "Reciclagem de repetidas",
        _ => motivo ?? origem
    };

    /// <summary>
    /// Texto do aviso no celular. Um pacote diz de onde veio ("Você ganhou 1 pacote pela vitória sobre o
    /// Grêmio"); vários viram um aviso só ("Você ganhou 4 pacotes: 3 pelos jogos e 1 do bolão").
    /// </summary>
    public static string AvisoDePacotes(IReadOnlyList<(string Origem, string? Motivo)> pacotes)
    {
        if (pacotes.Count == 0) return string.Empty;

        if (pacotes.Count == 1)
        {
            var (origem, motivo) = pacotes[0];
            return origem switch
            {
                PacoteGanho.OrigemJogo when motivo is not null => $"Você ganhou 1 pacote {PeloJogo(motivo)}.",
                PacoteGanho.OrigemDiaDeJogo => "Você ganhou 1 pacote pelo dia de jogo.",
                PacoteGanho.OrigemBolao when motivo is not null => $"Você ganhou 1 pacote pelos {motivo}.",
                PacoteGanho.OrigemAdmin when motivo is not null => $"Você ganhou 1 pacote da organização: {motivo}.",
                PacoteGanho.OrigemAdmin => "Você ganhou 1 pacote da organização.",
                _ => "Você ganhou 1 pacote."
            };
        }

        var origens = pacotes.Select(p => p.Origem).Distinct().ToList();
        if (origens.Count == 1)
        {
            // Os pacotes de um jogo só (3 pela vitória, 2 pelo empate) dizem qual foi o jogo.
            var motivos = pacotes.Select(p => p.Motivo).Distinct().ToList();
            if (origens[0] == PacoteGanho.OrigemJogo && motivos is [{ } motivo])
                return $"Você ganhou {pacotes.Count} pacotes {PeloJogo(motivo)}.";

            var de = origens[0] switch
            {
                PacoteGanho.OrigemJogo => " pelos jogos",
                PacoteGanho.OrigemDiaDeJogo => " pelos dias de jogo",
                PacoteGanho.OrigemBolao => " do bolão",
                PacoteGanho.OrigemAdmin => " da organização",
                _ => ""
            };
            return $"Você ganhou {pacotes.Count} pacotes{de}.";
        }

        var partes = pacotes
            .GroupBy(p => p.Origem)
            .OrderBy(g => g.Key switch
            {
                PacoteGanho.OrigemJogo => 0,
                PacoteGanho.OrigemDiaDeJogo => 1,
                PacoteGanho.OrigemBolao => 2,
                PacoteGanho.OrigemAdmin => 3,
                _ => 4
            })
            .Select(g => (g.Key, g.Count()) switch
            {
                (PacoteGanho.OrigemJogo, 1) => "1 pelo jogo",
                (PacoteGanho.OrigemJogo, var n) => $"{n} pelos jogos",
                (PacoteGanho.OrigemDiaDeJogo, 1) => "1 pelo dia de jogo",
                (PacoteGanho.OrigemDiaDeJogo, var n) => $"{n} pelos dias de jogo",
                (PacoteGanho.OrigemBolao, var n) => $"{n} do bolão",
                (PacoteGanho.OrigemAdmin, var n) => $"{n} da organização",
                (_, var n) => $"{n} {(n == 1 ? "outro" : "outros")}"
            })
            .ToList();

        var detalhe = partes.Count == 1
            ? partes[0]
            : string.Join(", ", partes.Take(partes.Count - 1)) + " e " + partes[^1];
        return $"Você ganhou {pacotes.Count} pacotes: {detalhe}.";
    }

    /// <summary>
    /// Texto do aviso de selo: o álbum completo fala mais alto; páginas viram "Página do Santos completa!" ou,
    /// várias de uma vez, "3 páginas completas: Santos, Grêmio e Vasco."
    /// </summary>
    public static string AvisoDeConquistas(string albumNome, bool albumCompleto, IReadOnlyList<string> paginas)
    {
        if (albumCompleto) return $"🏆 Você completou o {albumNome}! Seu nome está no Hall da Fama.";
        if (paginas.Count == 0) return string.Empty;
        if (paginas.Count == 1) return $"🏅 Página do {paginas[0]} completa!";
        var lista = string.Join(", ", paginas.Take(paginas.Count - 1)) + " e " + paginas[^1];
        return $"🏅 {paginas.Count} páginas completas: {lista}.";
    }

    /// <summary>
    /// Frase do compartilhamento da carta: "Tirei o Totti lendário no Álbum CBFV 2010!".
    /// </summary>
    public static string FraseDeCompartilhar(FigurinhaDto f, string albumNome) => (f.Tipo, f.Raridade) switch
    {
        (TipoFigurinha.Escudo, _) => $"Colei o escudo brilhante do {f.TimeNome} no {albumNome}!",
        (TipoFigurinha.Treinador, _) => $"Tirei o {(f.Papel == PapelTreinador.Auxiliar ? "auxiliar" : "técnico")} {f.Perfil?.NomeCurto ?? f.NomeImpresso} do {f.TimeNome} no {albumNome}!",
        (_, RaridadeFigurinha.Lendaria) => $"Tirei o {f.NomeImpresso} lendário no {albumNome}!",
        (_, RaridadeFigurinha.Brilhante) => $"Tirei o {f.NomeImpresso} brilhante no {albumNome}!",
        _ => $"Colei o {f.NomeImpresso} no {albumNome}!"
    };

    /// <summary>A melhor carta do pacote para compartilhar: a mais rara, a nova antes da repetida, o maior overall.</summary>
    public static FigurinhaDto MelhorDoPacote(IReadOnlyList<(FigurinhaDto Figurinha, bool Nova)> cartas) =>
        cartas
            .OrderByDescending(c => c.Figurinha.Raridade)
            .ThenByDescending(c => c.Figurinha.Tipo == TipoFigurinha.Jogador)
            .ThenByDescending(c => c.Nova)
            .ThenByDescending(c => c.Figurinha.Overall ?? 0)
            .First().Figurinha;

    /// <summary>
    /// Texto do aviso de troca para quem precisa saber: proposta (ou contraproposta) recebida, aceita ou
    /// recusada. Vários de uma vez viram "3 novidades nas suas trocas".
    /// </summary>
    public static string AvisoDeTrocas(IReadOnlyList<(string Evento, string Quem, int Oferecidas, int Pedidas)> eventos)
    {
        if (eventos.Count == 0) return string.Empty;
        if (eventos.Count > 1) return $"🔁 {eventos.Count} novidades nas suas trocas de figurinhas.";

        var (evento, quem, oferecidas, pedidas) = eventos[0];
        return evento switch
        {
            EventoTroca.Recebida => $"🔁 {quem} te propôs uma troca: {Figurinhas(oferecidas)} dele por {pedidas} sua{(pedidas == 1 ? "" : "s")}.",
            EventoTroca.Contraproposta => $"🔁 {quem} fez uma contraproposta: {Figurinhas(oferecidas)} dele por {pedidas} sua{(pedidas == 1 ? "" : "s")}.",
            EventoTroca.Aceita => $"✅ {quem} aceitou a sua troca. As figurinhas já estão no seu álbum.",
            EventoTroca.Recusada => $"❌ {quem} recusou a sua troca.",
            _ => "🔁 Novidade nas suas trocas de figurinhas."
        };
    }

    private static string Figurinhas(int n) => n == 1 ? "1 figurinha" : $"{n} figurinhas";

    /// <summary>Os acontecimentos de uma troca que viram aviso no celular.</summary>
    public static class EventoTroca
    {
        public const string Recebida = "recebida";
        public const string Contraproposta = "contraproposta";
        public const string Aceita = "aceita";
        public const string Recusada = "recusada";
    }

    private static string Minuscula(string texto) =>
        texto.Length == 0 ? texto : char.ToLowerInvariant(texto[0]) + texto[1..];
}
