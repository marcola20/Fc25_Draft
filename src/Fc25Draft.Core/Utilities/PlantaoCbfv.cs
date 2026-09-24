using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

public enum PlantaoCategoria
{
    Jogo,
    Mercado
}

/// <summary>Uma notícia do Plantão: manchete, linha fina e para onde o clique leva.</summary>
public record PlantaoNoticiaDto(
    DateTime Data,
    PlantaoCategoria Categoria,
    string Emoji,
    string Manchete,
    string? Detalhe,
    string Link,
    // Time para o escudo ao lado da notícia.
    string? TimeNome);

public record PlantaoPartidaInput(
    Guid PartidaId, Guid LigaId, string Competicao, string Etapa, bool Final,
    Guid CasaId, Guid ForaId, int GolsCasa, int GolsFora,
    bool IsWO, bool TemPenaltis, Guid? PenaltisVencedorId, DateTime EncerradaEm);

public record PlantaoGolInput(Guid PartidaId, Guid TimeId, int JogadorId, string JogadorNome, int? Minuto, bool Contra);

/// <summary>Título decidido na tabela (liga sem final): a data é a do último jogo da competição.</summary>
public record PlantaoTituloInput(Guid LigaId, string Competicao, Guid TimeId, DateTime Data);

public record PlantaoTransferenciaInput(
    DateTime Data, TransferType Tipo, int PlayerId, string JogadorNome, Guid? DeTimeId, Guid? ParaTimeId, decimal? Valor);

/// <summary>
/// Gera as notícias do Plantão CBFV a partir de jogos, gols e transferências.
/// As manchetes variam de verbo conforme o jogo, sempre do mesmo jeito para o mesmo jogo.
/// </summary>
public static class PlantaoCbfv
{
    public static IReadOnlyList<PlantaoNoticiaDto> Gerar(
        IEnumerable<PlantaoPartidaInput> partidas,
        IEnumerable<PlantaoGolInput> gols,
        IEnumerable<PlantaoTransferenciaInput> transferencias,
        IEnumerable<PlantaoTituloInput> titulosNaTabela,
        IReadOnlyDictionary<Guid, Guid?> campeaoPorLiga,
        IReadOnlyDictionary<Guid, string> nomes)
    {
        string Nome(Guid? id) => id is Guid g && nomes.TryGetValue(g, out var n) ? n : "?";

        var golsPorPartida = gols.GroupBy(g => g.PartidaId).ToDictionary(g => g.Key, g => g.ToList());
        var noticias = new List<PlantaoNoticiaDto>();

        foreach (var p in partidas)
        {
            var dela = golsPorPartida.GetValueOrDefault(p.PartidaId) ?? new List<PlantaoGolInput>();
            var link = $"/teams/details/{p.CasaId}";
            var competicao = $"{p.Competicao} · {p.Etapa}";

            var casa = Nome(p.CasaId);
            var fora = Nome(p.ForaId);
            var diferenca = Math.Abs(p.GolsCasa - p.GolsFora);

            // Vencedor: pelo placar ou, empatado, pelos pênaltis.
            Guid? vencedorId = p.GolsCasa > p.GolsFora ? p.CasaId
                : p.GolsFora > p.GolsCasa ? p.ForaId
                : p.TemPenaltis ? p.PenaltisVencedorId
                : null;
            var vencedor = vencedorId is null ? null : Nome(vencedorId);
            var perdedor = vencedorId is null ? null : Nome(vencedorId == p.CasaId ? p.ForaId : p.CasaId);
            var placar = vencedorId == p.ForaId ? $"{p.GolsFora} x {p.GolsCasa}" : $"{p.GolsCasa} x {p.GolsFora}";

            string emoji;
            string manchete;
            if (p.Final && vencedorId is Guid campeao && campeaoPorLiga.GetValueOrDefault(p.LigaId) == campeao)
            {
                emoji = "🏆";
                manchete = p.GolsCasa == p.GolsFora
                    ? $"{vencedor} é campeão {DaDo(p.Competicao)} nos pênaltis!"
                    : $"{vencedor} bate o {perdedor} por {placar} e é campeão {DaDo(p.Competicao)}!";
            }
            else if (p.IsWO && vencedor is not null)
            {
                emoji = "📋";
                manchete = $"{vencedor} vence o {perdedor} por W.O.";
            }
            else if (vencedor is null)
            {
                emoji = "🤝";
                manchete = p.GolsCasa == 0
                    ? $"{casa} e {fora} ficam no 0 x 0"
                    : Escolher(p.PartidaId, $"{casa} e {fora} empatam em {placar}", $"Tudo igual: {casa} {placar} {fora}");
            }
            else if (p.GolsCasa == p.GolsFora)
            {
                emoji = "🎯";
                manchete = $"{vencedor} passa pelo {perdedor} nos pênaltis";
            }
            else if (diferenca >= 3)
            {
                emoji = "💥";
                manchete = Escolher(p.PartidaId,
                    $"{vencedor} goleia o {perdedor} por {placar}",
                    $"{vencedor} atropela o {perdedor}: {placar}",
                    $"Show do {vencedor}: {placar} no {perdedor}");
            }
            else if (vencedorId == p.ForaId)
            {
                emoji = "✈️";
                manchete = Escolher(p.PartidaId,
                    $"{vencedor} vence o {perdedor} fora de casa por {placar}",
                    $"{vencedor} busca a vitória na casa do {perdedor}: {placar}",
                    $"{vencedor} cala a torcida do {perdedor} e vence por {placar}");
            }
            else
            {
                emoji = "⚽";
                manchete = Escolher(p.PartidaId,
                    $"{vencedor} vence o {perdedor} por {placar}",
                    $"{vencedor} bate o {perdedor} por {placar}",
                    $"{vencedor} derrota o {perdedor}: {placar}");
            }

            noticias.Add(new PlantaoNoticiaDto(p.EncerradaEm, PlantaoCategoria.Jogo, emoji, manchete,
                Detalhe(competicao, dela, p, Nome), link, vencedor ?? casa));

            // Hat-trick vira notícia própria.
            foreach (var h in dela.Where(g => !g.Contra).GroupBy(g => g.JogadorId).Where(g => g.Count() >= 3))
            {
                var autor = h.First();
                var doTime = Nome(autor.TimeId);
                var rival = Nome(autor.TimeId == p.CasaId ? p.ForaId : p.CasaId);
                var resultado = vencedorId == autor.TimeId ? "na vitória do" : vencedorId is null ? "no empate do" : "mesmo na derrota do";
                noticias.Add(new PlantaoNoticiaDto(p.EncerradaEm.AddSeconds(1), PlantaoCategoria.Jogo, "🎩",
                    $"{autor.JogadorNome} marca {h.Count()} vezes {resultado} {doTime} contra o {rival}",
                    competicao, $"/players/details/{autor.JogadorId}", doTime));
            }
        }

        foreach (var t in titulosNaTabela)
        {
            noticias.Add(new PlantaoNoticiaDto(t.Data.AddSeconds(2), PlantaoCategoria.Jogo, "🏆",
                $"{Nome(t.TimeId)} é campeão {DaDo(t.Competicao)}!", "Campeão na tabela",
                $"/teams/details/{t.TimeId}", Nome(t.TimeId)));
        }

        foreach (var t in transferencias)
        {
            var valor = t.Valor is decimal v && v > 0 ? $" por {Moeda(v)}" : "";
            var noticia = t.Tipo switch
            {
                TransferType.MarketAuction => new PlantaoNoticiaDto(t.Data, PlantaoCategoria.Mercado, "💸",
                    $"{Nome(t.ParaTimeId)} contrata {t.JogadorNome}{valor}", "Leilão do mercado",
                    $"/players/details/{t.PlayerId}", Nome(t.ParaTimeId)),
                TransferType.TeamSale => new PlantaoNoticiaDto(t.Data, PlantaoCategoria.Mercado, "🤝",
                    $"{t.JogadorNome} troca o {Nome(t.DeTimeId)} pelo {Nome(t.ParaTimeId)}{valor}", "Venda entre times",
                    $"/players/details/{t.PlayerId}", Nome(t.ParaTimeId)),
                TransferType.TeamTrade => new PlantaoNoticiaDto(t.Data, PlantaoCategoria.Mercado, "🔄",
                    $"{t.JogadorNome} vai do {Nome(t.DeTimeId)} para o {Nome(t.ParaTimeId)} em troca", "Troca entre times",
                    $"/players/details/{t.PlayerId}", Nome(t.ParaTimeId)),
                _ => null
            };

            if (noticia is not null)
                noticias.Add(noticia);
        }

        return noticias.OrderByDescending(n => n.Data).ToList();
    }

    /// <summary>Linha fina do jogo: competição e quem marcou de cada lado.</summary>
    private static string Detalhe(string competicao, List<PlantaoGolInput> gols, PlantaoPartidaInput p, Func<Guid?, string> nome)
    {
        if (gols.Count == 0)
            return competicao;

        string Lado(Guid timeId) => string.Join(", ", gols
            .Where(g => g.TimeId == timeId)
            .GroupBy(g => (g.JogadorId, g.Contra))
            .Select(g => g.First().JogadorNome + (g.Key.Contra ? " (contra)" : "") + (g.Count() > 1 ? $" {g.Count()}x" : "")));

        var partes = new[] { (p.CasaId, Lado(p.CasaId)), (p.ForaId, Lado(p.ForaId)) }
            .Where(x => x.Item2.Length > 0)
            .Select(x => $"{nome(x.Item1)}: {x.Item2}");

        return $"{competicao} · ⚽ {string.Join(" | ", partes)}";
    }

    // "do Brasileirão CBFV 2009", "da Copa do Brasil".
    private static string DaDo(string competicao) =>
        (competicao.StartsWith("Brasileir", StringComparison.OrdinalIgnoreCase)
         || competicao.StartsWith("Campeonato", StringComparison.OrdinalIgnoreCase)
            ? "do " : "da ") + competicao;

    // Mesmo jogo, mesma manchete: a escolha vem do id, não de sorteio.
    private static string Escolher(Guid id, params string[] opcoes) =>
        opcoes[(int)((uint)id.GetHashCode() % (uint)opcoes.Length)];

    private static string Moeda(decimal valor) =>
        valor >= 1_000_000
            ? $"R$ {(valor / 1_000_000).ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))} mi"
            : valor.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
}
