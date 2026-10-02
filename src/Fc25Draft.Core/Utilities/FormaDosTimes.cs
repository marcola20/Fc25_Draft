using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>Um jogo na forma recente: 'V', 'E' ou 'D', e o placar para a dica ("V 2x1 Flamengo").</summary>
public sealed record ResultadoForma(char Resultado, string Descricao);

/// <summary>Os últimos jogos de cada time numa competição, do mais antigo para o mais recente.</summary>
public static class FormaDosTimes
{
    public static IReadOnlyDictionary<Guid, IReadOnlyList<ResultadoForma>> Calcular(
        IEnumerable<LigaRodadaComPartidasDto> rodadas, int quantidade = 5)
    {
        var jogos = rodadas
            .SelectMany(r => r.Partidas)
            .Where(p => p.Status == PartidaStatus.Encerrada)
            .OrderBy(p => p.EncerradaEm ?? DateTime.MinValue)
            .ThenBy(p => p.RodadaNumero)
            .ToList();

        var forma = new Dictionary<Guid, List<ResultadoForma>>();
        foreach (var p in jogos)
        {
            Anotar(p.TimeCasaId, p.GolsCasa, p.GolsFora, p.TimeForaNome, p.TemPenaltis ? p.PenaltisVencedorId : null);
            Anotar(p.TimeForaId, p.GolsFora, p.GolsCasa, p.TimeCasaNome, p.TemPenaltis ? p.PenaltisVencedorId : null);
        }

        return forma.ToDictionary(f => f.Key, f => (IReadOnlyList<ResultadoForma>)f.Value.TakeLast(quantidade).ToList());

        void Anotar(Guid time, int pro, int contra, string adversario, Guid? penaltis)
        {
            // Empate decidido nos pênaltis conta como empate, com a dica dizendo quem passou.
            var resultado = pro > contra ? 'V' : pro < contra ? 'D' : 'E';
            var extra = penaltis is Guid v ? (v == time ? " (venceu nos pênaltis)" : " (perdeu nos pênaltis)") : "";
            if (!forma.TryGetValue(time, out var lista)) forma[time] = lista = new List<ResultadoForma>();
            lista.Add(new ResultadoForma(resultado, $"{resultado} {pro}x{contra} {adversario}{extra}"));
        }
    }
}
