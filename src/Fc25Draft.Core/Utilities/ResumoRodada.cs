using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>Uma partida no card da rodada, com quem marcou de cada lado (ex.: "Higuaín 12', 45'").</summary>
public record RodadaResumoJogoDto(LigaPartidaDto Partida, IReadOnlyList<string> GolsCasa, IReadOnlyList<string> GolsFora);

public record RodadaResumoArtilheiroDto(int JogadorId, string Nome, string TimeNome, int Gols);

public record RodadaResumoDto(
    IReadOnlyList<RodadaResumoJogoDto> Jogos,
    int JogosEncerrados,
    int TotalGols,
    // Quem mais marcou na rodada; vazio se ninguém fez 2 ou mais.
    IReadOnlyList<RodadaResumoArtilheiroDto> Artilheiros,
    // Maior diferença de gols; nulo se nenhum jogo terminou com 2 ou mais de diferença.
    LigaPartidaDto? MaiorGoleada);

/// <summary>Monta o resumo de uma rodada a partir das partidas e dos gols dela.</summary>
public static class ResumoRodada
{
    public static RodadaResumoDto Calcular(IReadOnlyList<LigaPartidaDto> partidas, IEnumerable<LigaEventoDto> gols)
    {
        // Gol contra vem com o TimeId de quem foi beneficiado: fica do lado que ganhou o gol.
        var golsPorPartida = gols
            .Where(g => g.Tipo is TipoEvento.Gol or TipoEvento.GolContra)
            .GroupBy(g => g.PartidaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var jogos = partidas
            .Select(p =>
            {
                var dela = golsPorPartida.GetValueOrDefault(p.PartidaId) ?? new List<LigaEventoDto>();
                return new RodadaResumoJogoDto(
                    p,
                    Linhas(dela.Where(g => g.TimeId == p.TimeCasaId)),
                    Linhas(dela.Where(g => g.TimeId == p.TimeForaId)));
            })
            .ToList();

        var encerradas = partidas.Where(p => p.Status == PartidaStatus.Encerrada).ToList();

        var porJogador = gols
            .Where(g => g.Tipo == TipoEvento.Gol)
            .GroupBy(g => g.JogadorId)
            .Select(g => new RodadaResumoArtilheiroDto(g.Key, g.First().JogadorNome, g.First().TimeNome, g.Count()))
            .ToList();
        var maxGols = porJogador.Count == 0 ? 0 : porJogador.Max(a => a.Gols);
        var artilheiros = maxGols >= 2
            ? porJogador.Where(a => a.Gols == maxGols).OrderBy(a => a.Nome, StringComparer.OrdinalIgnoreCase).ToList()
            : new List<RodadaResumoArtilheiroDto>();

        var maiorGoleada = encerradas
            .Where(p => Math.Abs(p.GolsCasa - p.GolsFora) >= 2)
            .OrderByDescending(p => Math.Abs(p.GolsCasa - p.GolsFora))
            .ThenByDescending(p => p.GolsCasa + p.GolsFora)
            .FirstOrDefault();

        return new RodadaResumoDto(
            jogos,
            encerradas.Count,
            encerradas.Sum(p => p.GolsCasa + p.GolsFora),
            artilheiros,
            maiorGoleada);
    }

    /// <summary>Um item por jogador, com os minutos: "Higuaín 12', 45'", "Fulano (contra) 30'".</summary>
    private static IReadOnlyList<string> Linhas(IEnumerable<LigaEventoDto> gols) =>
        gols
            .GroupBy(g => (g.JogadorId, Contra: g.Tipo == TipoEvento.GolContra))
            .Select(g =>
            {
                var nome = g.First().JogadorNome + (g.Key.Contra ? " (contra)" : "");
                var minutos = g.Where(x => x.Minuto is not null).Select(x => $"{x.Minuto}'").ToList();
                var semMinuto = g.Count(x => x.Minuto is null);
                var sufixo = minutos.Count > 0
                    ? " " + string.Join(", ", minutos) + (semMinuto > 0 ? $" +{semMinuto}" : "")
                    : g.Count() > 1 ? $" ({g.Count()}x)" : "";
                return nome + sufixo;
            })
            .ToList();
}
