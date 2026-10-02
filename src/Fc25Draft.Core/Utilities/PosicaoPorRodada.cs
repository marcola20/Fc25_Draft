using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>Onde o time estava ao fim de uma rodada e com quantos pontos.</summary>
public sealed record PosicaoNaRodada(int Rodada, int Posicao, int Pontos);

/// <summary>
/// Refaz a tabela da Liga ao fim de cada rodada, com os jogos disputados até ela (mesma conta do
/// recálculo oficial: 3/1/0, sem jogo decisivo e sem mata-mata). A última rodada usa a posição da
/// tabela oficial, para o gráfico terminar igual à classificação.
/// </summary>
public static class PosicaoPorRodada
{
    public static IReadOnlyDictionary<Guid, IReadOnlyList<PosicaoNaRodada>> Calcular(
        IEnumerable<LigaRodadaComPartidasDto> rodadas,
        IReadOnlyList<LigaClassificacaoItemDto> tabela,
        Func<LigaClassificacaoItemDto, int> posicaoOficial)
    {
        var regulares = rodadas
            .Where(r => r.Numero > 0 && !r.Desempate)
            .OrderBy(r => r.Numero)
            .ToList();

        var disputadas = regulares
            .Where(r => r.Partidas.Any(p => p.Status != PartidaStatus.Agendada))
            .Select(r => r.Numero)
            .ToList();
        if (disputadas.Count == 0 || tabela.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<PosicaoNaRodada>>();

        var ultima = disputadas.Max();
        // Punição não tem rodada: vale a temporada inteira, como na tabela.
        var stats = tabela.ToDictionary(t => t.TimeId, t => new Acumulado { Pontos = -t.PontosDescontados });
        var confrontos = new List<ConfrontoDireto>();
        var resultado = tabela.ToDictionary(t => t.TimeId, _ => new List<PosicaoNaRodada>());

        foreach (var rodada in regulares.Where(r => r.Numero <= ultima))
        {
            foreach (var p in rodada.Partidas.Where(p => p.Status != PartidaStatus.Agendada))
            {
                if (!stats.TryGetValue(p.TimeCasaId, out var casa) || !stats.TryGetValue(p.TimeForaId, out var fora))
                    continue;
                casa.Somar(p.GolsCasa, p.GolsFora);
                fora.Somar(p.GolsFora, p.GolsCasa);
                if (p.Status == PartidaStatus.Encerrada && !p.IsWO)
                    confrontos.Add(new ConfrontoDireto(p.TimeCasaId, p.TimeForaId, p.GolsCasa, p.GolsFora));
            }

            if (rodada.Numero == ultima)
            {
                foreach (var t in tabela)
                    resultado[t.TimeId].Add(new PosicaoNaRodada(rodada.Numero, posicaoOficial(t), stats[t.TimeId].Pontos));
                break;
            }

            var ordem = LigaDesempate.Ordenar(tabela, TipoCompetition.Liga, t => t.TimeId,
                t => stats[t.TimeId].Desempate, confrontos);
            for (var i = 0; i < ordem.Count; i++)
                resultado[ordem[i].TimeId].Add(new PosicaoNaRodada(rodada.Numero, i + 1, stats[ordem[i].TimeId].Pontos));
        }

        return resultado.ToDictionary(r => r.Key, r => (IReadOnlyList<PosicaoNaRodada>)r.Value);
    }

    private sealed class Acumulado
    {
        public int Pontos, Vitorias, GolsPro, GolsContra;

        public DesempateStats Desempate => new(Pontos, Vitorias, GolsPro - GolsContra, GolsPro);

        public void Somar(int pro, int contra)
        {
            Pontos += pro > contra ? 3 : pro == contra ? 1 : 0;
            if (pro > contra) Vitorias++;
            GolsPro += pro;
            GolsContra += contra;
        }
    }
}
