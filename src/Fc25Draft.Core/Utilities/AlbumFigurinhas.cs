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
}
