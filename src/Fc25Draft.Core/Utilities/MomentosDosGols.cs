using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Utilities;

/// <summary>Uma partida encerrada da competição (sem W.O.), com o rótulo da etapa.</summary>
public record MomentoPartidaInput(Guid PartidaId, string Rotulo, Guid CasaId, Guid ForaId, int GolsCasa, int GolsFora);

/// <summary>Um gol da partida; TimeId é quem ganhou o gol (no gol contra, o adversário do autor).</summary>
public record MomentoGolInput(Guid PartidaId, Guid TimeId, int JogadorId, string JogadorNome, int? Minuto, DateTime CriadoEm, bool Contra);

/// <summary>
/// Gols decisivos, viradas e gols por período a partir da ordem dos gols. Jogo cujos gols não têm minuto ou não
/// fecham com o placar (temporadas antigas, lançamento incompleto) fica de fora para não inventar a ordem.
/// </summary>
public static class MomentosDosGols
{
    public static readonly IReadOnlyList<string> Periodos = ["1–15'", "16–30'", "31–45'", "46–60'", "61–75'", "76–90'+"];

    /// <summary>Período de 15 minutos do gol; acréscimos do segundo tempo ficam no último.</summary>
    public static int Periodo(int minuto) => Math.Clamp((minuto - 1) / 15, 0, Periodos.Count - 1);

    public static MomentosDosGolsDto Calcular(
        IReadOnlyCollection<MomentoPartidaInput> partidas,
        IEnumerable<MomentoGolInput> gols,
        IReadOnlyDictionary<Guid, string> nomes)
    {
        // Competição sem minuto em nenhum gol (2008): nem os 0x0 entram, senão as tabelas saem vazias.
        var golsPorPartida = gols.GroupBy(g => g.PartidaId).ToDictionary(g => g.Key, g => g.ToList());
        if (!golsPorPartida.Values.Any(l => l.Any(g => g.Minuto is not null)))
            return new MomentosDosGolsDto(0, partidas.Count, [], [], [], Periodos, []);
        string Nome(Guid timeId) => nomes.GetValueOrDefault(timeId, "?");

        var decisivos = new List<(MomentoGolInput Gol, bool DaVitoria, MomentoPartidaInput Partida, int Casa, int Fora)>();
        var viradas = new List<ViradaJogoDto>();
        var times = new Dictionary<Guid, (int Viradas, int Empates, int Buscados, int Perdidas, int Perdidos)>();
        var jogosPorTime = new Dictionary<Guid, List<ViradaTimeJogoDto>>();
        var feitos = new Dictionary<Guid, int[]>();
        var sofridos = new Dictionary<Guid, int[]>();
        var analisados = 0;

        // Soma o jogo nos números do time e guarda o jogo para a lista dele.
        void Registrar(Guid timeId, MomentoPartidaInput p, TipoViradaJogo tipo, int gols, int pontos)
        {
            var t = times[timeId];
            times[timeId] = (
                t.Viradas + (tipo == TipoViradaJogo.Virada ? 1 : 0),
                t.Empates + (tipo == TipoViradaJogo.EmpateBuscado ? 1 : 0),
                t.Buscados + Math.Max(pontos, 0),
                t.Perdidas + (tipo == TipoViradaJogo.VantagemPerdida ? 1 : 0),
                t.Perdidos + Math.Max(-pontos, 0));
            jogosPorTime[timeId].Add(new ViradaTimeJogoDto(p.PartidaId, p.Rotulo, Nome(p.CasaId), Nome(p.ForaId),
                p.GolsCasa, p.GolsFora, tipo, gols, pontos));
        }

        foreach (var p in partidas)
        {
            var lista = golsPorPartida.GetValueOrDefault(p.PartidaId) ?? [];
            if (lista.Any(g => g.Minuto is null)
                || lista.Count(g => g.TimeId == p.CasaId) != p.GolsCasa
                || lista.Count(g => g.TimeId == p.ForaId) != p.GolsFora)
                continue;

            analisados++;
            foreach (var t in new[] { p.CasaId, p.ForaId })
            {
                feitos.TryAdd(t, new int[Periodos.Count]);
                sofridos.TryAdd(t, new int[Periodos.Count]);
                times.TryAdd(t, default);
                jogosPorTime.TryAdd(t, new List<ViradaTimeJogoDto>());
            }

            // Placar gol a gol: até quanto cada lado esteve na frente (saldo do mandante).
            var ordem = lista.OrderBy(g => g.Minuto).ThenBy(g => g.CriadoEm).ToList();
            int casa = 0, fora = 0, menorSaldo = 0, maiorSaldo = 0;
            var placarApos = new List<(int Casa, int Fora)>();
            foreach (var g in ordem)
            {
                if (g.TimeId == p.CasaId) casa++; else fora++;
                placarApos.Add((casa, fora));
                menorSaldo = Math.Min(menorSaldo, casa - fora);
                maiorSaldo = Math.Max(maiorSaldo, casa - fora);

                var periodo = Periodo(g.Minuto!.Value);
                feitos[g.TimeId][periodo]++;
                sofridos[g.TimeId == p.CasaId ? p.ForaId : p.CasaId][periodo]++;
            }

            if (p.GolsCasa != p.GolsFora)
            {
                var casaVenceu = p.GolsCasa > p.GolsFora;
                var (vencedor, perdedor) = casaVenceu ? (p.CasaId, p.ForaId) : (p.ForaId, p.CasaId);
                var desvantagem = casaVenceu ? -menorSaldo : maiorSaldo;
                if (desvantagem > 0)
                {
                    Registrar(vencedor, p, TipoViradaJogo.Virada, desvantagem, 3);
                    Registrar(perdedor, p, TipoViradaJogo.VantagemPerdida, desvantagem, -3);
                    viradas.Add(new ViradaJogoDto(p.PartidaId, p.Rotulo, p.CasaId, Nome(p.CasaId), p.ForaId, Nome(p.ForaId),
                        p.GolsCasa, p.GolsFora, vencedor, desvantagem));
                }

                // Gol da vitória: o que deixou o vencedor na frente de vez, só quando ele ganhou por um gol.
                var golsPerdedor = Math.Min(p.GolsCasa, p.GolsFora);
                if (Math.Abs(p.GolsCasa - p.GolsFora) == 1)
                {
                    var indice = Enumerable.Range(0, ordem.Count).Where(i => ordem[i].TimeId == vencedor).ElementAt(golsPerdedor);
                    decisivos.Add((ordem[indice], true, p, placarApos[indice].Casa, placarApos[indice].Fora));
                }
            }
            else if (ordem.Count > 0)
            {
                // Empate com gols: quem esteve atrás buscou 1 ponto; quem esteve na frente deixou 2 escaparem.
                if (menorSaldo < 0)
                {
                    Registrar(p.CasaId, p, TipoViradaJogo.EmpateBuscado, -menorSaldo, 1);
                    Registrar(p.ForaId, p, TipoViradaJogo.VantagemPerdida, -menorSaldo, -2);
                }
                if (maiorSaldo > 0)
                {
                    Registrar(p.ForaId, p, TipoViradaJogo.EmpateBuscado, maiorSaldo, 1);
                    Registrar(p.CasaId, p, TipoViradaJogo.VantagemPerdida, maiorSaldo, -2);
                }

                // Gol do empate: o último do jogo, que deixou tudo igual.
                decisivos.Add((ordem[^1], false, p, p.GolsCasa, p.GolsFora));
            }
        }

        var porJogador = decisivos
            .Where(d => !d.Gol.Contra)
            .GroupBy(d => d.Gol.JogadorId)
            .Select(g =>
            {
                var ultimo = g.OrderBy(d => d.Gol.CriadoEm).Last().Gol;
                return new GolDecisivoJogadorDto(g.Key, ultimo.JogadorNome, ultimo.TimeId, Nome(ultimo.TimeId),
                    g.Count(d => d.DaVitoria), g.Count(d => !d.DaVitoria),
                    g.Select(d => new GolDecisivoJogoDto(d.Partida.PartidaId, d.Partida.Rotulo,
                            Nome(d.Partida.CasaId), Nome(d.Partida.ForaId), d.Partida.GolsCasa, d.Partida.GolsFora,
                            d.Gol.Minuto!.Value, d.Casa, d.Fora, d.DaVitoria))
                        .ToList());
            })
            .OrderByDescending(d => d.Total)
            .ThenByDescending(d => d.DaVitoria)
            .ThenBy(d => d.JogadorNome, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var porTime = times
            .Select(t => new ViradaTimeDto(t.Key, Nome(t.Key), t.Value.Viradas, t.Value.Empates, t.Value.Buscados,
                t.Value.Perdidas, t.Value.Perdidos, jogosPorTime[t.Key]))
            .OrderByDescending(t => t.Saldo)
            .ThenByDescending(t => t.PontosBuscados)
            .ThenBy(t => t.TimeNome, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var porPeriodo = feitos.Keys
            .Select(t => new GolsPorPeriodoTimeDto(t, Nome(t), feitos[t], sofridos[t]))
            .OrderByDescending(t => t.Feitos.Sum())
            .ThenBy(t => t.Sofridos.Sum())
            .ThenBy(t => t.TimeNome, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MomentosDosGolsDto(
            analisados,
            partidas.Count,
            porJogador,
            porTime,
            viradas.OrderByDescending(v => v.MaiorDesvantagem).ToList(),
            Periodos,
            porPeriodo);
    }
}
