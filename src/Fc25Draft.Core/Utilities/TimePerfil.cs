using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>Uma partida do time (em casa ou fora), já com os rótulos de competição e etapa.</summary>
public record TimePerfilPartidaInput(
    Guid PartidaId,
    string Competicao,
    string Etapa,
    Guid CasaId,
    Guid ForaId,
    int GolsCasa,
    int GolsFora,
    PartidaStatus Status,
    bool IsWO,
    bool TemPenaltis,
    Guid? PenaltisVencedorId,
    DateTime? EncerradaEm,
    // Ordem dos jogos ainda não disputados (competição mais antiga primeiro, depois a rodada).
    long OrdemAgenda,
    // Data marcada da rodada, quando o calendário já foi aplicado.
    DateTime? Quando = null);

/// <summary>
/// Forma, sequências, recordes e confrontos de um time a partir das partidas dele.
/// Mesmo critério do Ranking de Clubes: decisão por pênaltis conta como empate.
/// </summary>
public static class TimePerfil
{
    public const int JogosNaForma = 5;
    public const int UltimosJogosListados = 10;
    public const int ProximosJogosListados = 5;

    public static TimePerfilDto Calcular(
        Guid timeId,
        IEnumerable<TimePerfilPartidaInput> partidas,
        IReadOnlyDictionary<Guid, string> nomes)
    {
        var lista = partidas.Where(p => p.CasaId == timeId || p.ForaId == timeId).ToList();

        // Do mais antigo para o mais recente: as sequências dependem da ordem.
        var jogados = lista
            .Where(p => p.Status == PartidaStatus.Encerrada)
            .OrderBy(p => p.EncerradaEm ?? DateTime.MinValue)
            .ThenBy(p => p.OrdemAgenda)
            .Select(p => Jogo(timeId, p, nomes))
            .ToList();

        // Os próximos jogos saem na ordem do calendário; sem data marcada, cai na ordem da agenda.
        var proximos = lista
            .Where(p => p.Status != PartidaStatus.Encerrada)
            .OrderBy(p => p.Quando ?? DateTime.MaxValue)
            .ThenBy(p => p.OrdemAgenda)
            .Take(ProximosJogosListados)
            .Select(p => Jogo(timeId, p, nomes))
            .ToList();

        var recentes = Enumerable.Reverse(jogados).ToList();
        var (sequencia, tipo) = SequenciaAtual(recentes);

        var confrontos = jogados
            .GroupBy(j => j.AdversarioId)
            .Select(g => new TimeConfrontoDto(
                g.Key,
                g.First().AdversarioNome,
                g.Count(),
                g.Count(j => j.Resultado == "V"),
                g.Count(j => j.Resultado == "E"),
                g.Count(j => j.Resultado == "D"),
                g.Sum(j => j.GolsPro!.Value),
                g.Sum(j => j.GolsContra!.Value)))
            .OrderByDescending(c => c.Jogos)
            .ThenByDescending(c => c.Vitorias)
            .ThenBy(c => c.AdversarioNome, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Maior diferença; no empate, mais gols marcados e depois a mais recente.
        var maiorVitoria = recentes
            .Where(j => j.Resultado == "V")
            .OrderByDescending(j => j.GolsPro - j.GolsContra)
            .ThenByDescending(j => j.GolsPro)
            .FirstOrDefault();

        var maiorDerrota = recentes
            .Where(j => j.Resultado == "D")
            .OrderByDescending(j => j.GolsContra - j.GolsPro)
            .ThenByDescending(j => j.GolsContra)
            .FirstOrDefault();

        return new TimePerfilDto(
            timeId,
            jogados.Count,
            jogados.Count(j => j.Resultado == "V"),
            jogados.Count(j => j.Resultado == "E"),
            jogados.Count(j => j.Resultado == "D"),
            jogados.Sum(j => j.GolsPro!.Value),
            jogados.Sum(j => j.GolsContra!.Value),
            recentes.Take(JogosNaForma).ToList(),
            sequencia,
            tipo,
            maiorVitoria,
            maiorDerrota,
            MaiorSequencia(jogados, r => r == "V"),
            MaiorSequencia(jogados, r => r != "D"),
            recentes.Take(UltimosJogosListados).ToList(),
            proximos,
            confrontos);
    }

    private static TimePerfilJogoDto Jogo(Guid timeId, TimePerfilPartidaInput p, IReadOnlyDictionary<Guid, string> nomes)
    {
        var emCasa = p.CasaId == timeId;
        var adversarioId = emCasa ? p.ForaId : p.CasaId;
        var adversario = nomes.TryGetValue(adversarioId, out var nome) ? nome : "?";

        if (p.Status != PartidaStatus.Encerrada)
        {
            // Jogo por vir: a data é a marcada no calendário.
            return new TimePerfilJogoDto(p.PartidaId, p.Competicao, p.Etapa, adversarioId, adversario, emCasa,
                null, null, null, null, p.IsWO, p.Quando);
        }

        var pro = emCasa ? p.GolsCasa : p.GolsFora;
        var contra = emCasa ? p.GolsFora : p.GolsCasa;
        var resultado = pro > contra ? "V" : pro < contra ? "D" : "E";
        bool? venceuPenaltis = p.TemPenaltis && p.PenaltisVencedorId is Guid vencedor ? vencedor == timeId : null;

        return new TimePerfilJogoDto(p.PartidaId, p.Competicao, p.Etapa, adversarioId, adversario, emCasa,
            pro, contra, resultado, venceuPenaltis, p.IsWO, p.EncerradaEm);
    }

    /// <summary>
    /// Sequência que está valendo agora. Vitórias seguidas; se o time ficou mais tempo sem perder,
    /// mostra a invencibilidade. Na fase ruim, derrotas seguidas ou tempo sem vencer.
    /// </summary>
    private static (string? Texto, string? Tipo) SequenciaAtual(IReadOnlyList<TimePerfilJogoDto> recentes)
    {
        if (recentes.Count == 0)
        {
            return (null, null);
        }

        int Contar(Func<string?, bool> condicao) => recentes.TakeWhile(j => condicao(j.Resultado)).Count();

        var vitorias = Contar(r => r == "V");
        var invicto = Contar(r => r != "D");
        var derrotas = Contar(r => r == "D");
        var semVencer = Contar(r => r != "V");

        if (invicto > 0)
        {
            if (vitorias >= 2 && vitorias == invicto)
                return ($"{vitorias} vitórias seguidas", "V");
            if (invicto >= 2)
                return ($"Invicto há {invicto} jogos", vitorias > 0 ? "V" : "E");
            return (vitorias == 1 ? "Venceu o último jogo" : "Empatou o último jogo", vitorias == 1 ? "V" : "E");
        }

        if (derrotas >= 2 && derrotas == semVencer)
            return ($"{derrotas} derrotas seguidas", "D");
        if (semVencer >= 2)
            return ($"Sem vencer há {semVencer} jogos", "D");
        return ("Perdeu o último jogo", "D");
    }

    private static int MaiorSequencia(IEnumerable<TimePerfilJogoDto> jogosEmOrdem, Func<string?, bool> condicao)
    {
        var maior = 0;
        var atual = 0;
        foreach (var jogo in jogosEmOrdem)
        {
            atual = condicao(jogo.Resultado) ? atual + 1 : 0;
            maior = Math.Max(maior, atual);
        }

        return maior;
    }
}
