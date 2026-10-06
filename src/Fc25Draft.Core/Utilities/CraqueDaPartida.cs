using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>O craque de um jogo, com o time em que jogou e o que fez (gols e assistências).</summary>
public sealed record CraqueDaPartidaInfo(PartidaEscalacaoJogadorDto Jogador, Guid TimeId, string TimeNome, int Gols, int Assistencias);

/// <summary>
/// Quem foi o craque do jogo: o melhor em campo do PES; sem essa marca, o de maior nota (no empate, quem fez
/// mais gols). Sem nota nenhuma (jogo sem importação do PES), não há craque.
/// </summary>
public static class CraqueDaPartida
{
    public static CraqueDaPartidaInfo? Escolher(PartidaEscalacoesDto escalacoes)
    {
        var todos = new[] { escalacoes.Casa, escalacoes.Fora }
            .SelectMany(t => t.Jogadores.Select(j => (Time: t, Jogador: j)))
            .ToList();

        int Gols(int id) => escalacoes.Eventos.Count(e => e.Tipo == TipoEvento.Gol && e.JogadorId == id);
        int Assistencias(int id) => escalacoes.Eventos.Count(e => e.Tipo == TipoEvento.Gol && e.AssistenteId == id);

        var escolhido = todos.Where(x => x.Jogador.MelhorEmCampo).Select(x => ((PartidaEscalacaoTimeDto, PartidaEscalacaoJogadorDto)?)x).FirstOrDefault()
            ?? todos.Where(x => x.Jogador.Nota is not null)
                .OrderByDescending(x => x.Jogador.Nota)
                .ThenByDescending(x => Gols(x.Jogador.JogadorId))
                .Select(x => ((PartidaEscalacaoTimeDto, PartidaEscalacaoJogadorDto)?)x)
                .FirstOrDefault();
        if (escolhido is null) return null;
        var (time, jogador) = escolhido.Value;

        return new CraqueDaPartidaInfo(jogador, time.TimeId, time.TimeNome, Gols(jogador.JogadorId), Assistencias(jogador.JogadorId));
    }
}
