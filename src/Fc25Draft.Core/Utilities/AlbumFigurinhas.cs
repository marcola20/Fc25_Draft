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

    public sealed record ClubeDoAlbum(Guid TeamId, string Nome);

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
        int Ordem);

    /// <summary>
    /// Monta o álbum: uma página por clube, na ordem recebida, com o escudo e depois o elenco por posição
    /// (GOL, ZAG, LE… na ordem do <c>PositionType</c>) e, dentro da posição, por overall. Numeração corrida.
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

            var elenco = porClube[clube.TeamId].ToList();
            var brilhantes = MaioresOveralls(elenco.Select(j => (j.PlayerId, j.Overall, j.Nome)));

            var ordem = 0;
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

    /// <summary>Pacote do dia: um por pessoa por data de Brasília.</summary>
    public static string ChaveDiario(DateTime diaEmBrasilia) => $"diario:{diaEmBrasilia:yyyy-MM-dd}";

    public static string ChaveVitoria(Guid partidaId) => $"vitoria:{partidaId:N}";

    /// <summary>O n-ésimo pacote do bolão da temporada (1, 2, 3…).</summary>
    public static string ChaveBolao(int temporada, int n) => $"bolao:{temporada}:{n}";

    /// <summary>Quantos pacotes do bolão os pontos já valem.</summary>
    public static int PacotesDoBolao(int pontos, int pontosPorPacote) =>
        pontosPorPacote <= 0 || pontos <= 0 ? 0 : pontos / pontosPorPacote;

    public static string MotivoVitoria(string adversario) => $"Vitória sobre o {adversario}";

    public static string MotivoBolao(int pontos, int temporada) => $"{pontos} pontos no bolão de {temporada}";

    /// <summary>De onde veio o pacote, em poucas palavras, para a lista dos fechados.</summary>
    public static string DeOndeVeio(string origem, string? motivo) => origem switch
    {
        PacoteGanho.OrigemDiario => "Pacote do dia",
        PacoteGanho.OrigemVitoria => motivo ?? "Vitória",
        PacoteGanho.OrigemBolao => motivo is null ? "Bolão" : $"Bolão · {motivo}",
        PacoteGanho.OrigemAdmin => motivo is null ? "Da organização" : $"Da organização · {motivo}",
        _ => motivo ?? origem
    };

    /// <summary>
    /// Texto do aviso no celular. Um pacote diz de onde veio ("Você ganhou 1 pacote pela vitória sobre o
    /// Grêmio"); vários viram um aviso só ("Você ganhou 3 pacotes: 2 por vitórias e 1 do bolão").
    /// </summary>
    public static string AvisoDePacotes(IReadOnlyList<(string Origem, string? Motivo)> pacotes)
    {
        if (pacotes.Count == 0) return string.Empty;

        if (pacotes.Count == 1)
        {
            var (origem, motivo) = pacotes[0];
            return origem switch
            {
                PacoteGanho.OrigemVitoria when motivo is not null => $"Você ganhou 1 pacote pela {Minuscula(motivo)}.",
                PacoteGanho.OrigemBolao when motivo is not null => $"Você ganhou 1 pacote pelos {motivo}.",
                PacoteGanho.OrigemAdmin when motivo is not null => $"Você ganhou 1 pacote da organização: {motivo}.",
                PacoteGanho.OrigemAdmin => "Você ganhou 1 pacote da organização.",
                _ => "Você ganhou 1 pacote."
            };
        }

        var origens = pacotes.Select(p => p.Origem).Distinct().ToList();
        if (origens.Count == 1)
        {
            var de = origens[0] switch
            {
                PacoteGanho.OrigemVitoria => " por vitórias",
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
                PacoteGanho.OrigemVitoria => 0,
                PacoteGanho.OrigemBolao => 1,
                PacoteGanho.OrigemAdmin => 2,
                _ => 3
            })
            .Select(g => (g.Key, g.Count()) switch
            {
                (PacoteGanho.OrigemVitoria, 1) => "1 por vitória",
                (PacoteGanho.OrigemVitoria, var n) => $"{n} por vitórias",
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

    private static string Minuscula(string texto) =>
        texto.Length == 0 ? texto : char.ToLowerInvariant(texto[0]) + texto[1..];
}
