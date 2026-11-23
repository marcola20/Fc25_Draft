using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Copa em fase de liga (2011 em diante): todos numa tabela só, 4 jogos cada, sorteados em dois potes
/// pelo ranking da temporada anterior; 1º ao 4º vão às quartas e 5º ao 12º jogam o playoff.
/// Os times ficam todos no "grupo A", e assim a tabela, a classificação e os jogos decisivos seguem
/// pelo mesmo caminho da Copa com grupos.
/// </summary>
public partial class LigaAdminService
{
    /// <summary>O grupo único que guarda a tabela da fase de liga.</summary>
    private const GrupoCopa GrupoDaFaseDeLiga = GrupoCopa.A;

    /// <summary>Quantas posições a Copa anterior sobe o time no ranking do sorteio.</summary>
    private static int BonusDaCopa(FasePremiacao? fase) => fase switch
    {
        FasePremiacao.Campeao => 4,
        FasePremiacao.Vice => 3,
        FasePremiacao.Semifinal => 2,
        FasePremiacao.Quartas => 1,
        _ => 0
    };

    private static string NomeDaFaseNaCopa(FasePremiacao fase) => fase switch
    {
        FasePremiacao.Campeao => "campeão",
        FasePremiacao.Vice => "vice",
        FasePremiacao.Semifinal => "semifinal",
        FasePremiacao.Quartas => "quartas",
        FasePremiacao.Playoff => "playoff",
        _ => "primeira fase"
    };

    public async Task<LigaDto> DefinirFormatoCopaAsync(Guid ligaId, FormatoCopa formato, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (liga.Tipo != TipoCompetition.Copa)
            throw new InvalidOperationException("O formato só vale para a Copa.");
        if (liga.Status != LigaStatus.Criada
            || await _db.LigaRodadas.AnyAsync(r => r.LigaId == ligaId, ct)
            || await _db.LigaGruposTimes.AnyAsync(g => g.LigaId == ligaId, ct))
            throw new InvalidOperationException("O formato só muda antes do sorteio (sem grupos nem rodadas).");

        liga.FormatoCopa = formato;
        liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);
        return ToDto(liga);
    }

    public async Task<CopaFaseDeLigaSorteioDto?> GetSorteioFaseDeLigaAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(x => x.LigaId == ligaId, ct);
        if (liga is not { Tipo: TipoCompetition.Copa, FormatoCopa: FormatoCopa.FaseDeLiga }) return null;

        var ranking = await RankingDaFaseDeLigaAsync(liga, ct);
        var nomes = ranking.ToDictionary(r => r.TimeId, r => r.TimeNome);

        var jogos = (await _db.LigaPartidas.AsNoTracking()
                .Where(p => p.Rodada.LigaId == ligaId && p.Rodada.Numero > 0 && !p.Rodada.Desempate)
                .Select(p => new { p.Rodada.Numero, p.TimeCasaId, Casa = p.TimeCasa.TeamName, p.TimeForaId, Fora = p.TimeFora.TeamName })
                .ToListAsync(ct))
            .OrderBy(j => j.Numero).ThenBy(j => j.Casa)
            .Select(j => new CopaJogoSorteadoDto(j.Numero, j.TimeCasaId, j.Casa, j.TimeForaId, j.Fora))
            .ToList();

        var jaSorteada = jogos.Count > 0 && await _db.LigaGruposTimes.AnyAsync(g => g.LigaId == ligaId, ct);

        // Depois do sorteio, os potes gravados valem (o ranking pode ter mudado se alguém trocou de time).
        if (jaSorteada)
        {
            var potes = await _db.LigaCopaPotes.AsNoTracking().Where(p => p.LigaId == ligaId)
                .ToDictionaryAsync(p => p.TimeId, p => p.Pote, ct);
            ranking = ranking.Select(r => r with { Pote = potes.GetValueOrDefault(r.TimeId, r.Pote) }).ToList();
        }

        var impedimento =
            liga.Status != LigaStatus.Criada ? "A Copa já começou: o sorteio só vale antes de iniciar."
            : liga.Temporada is null ? "A Copa não tem temporada."
            : ranking.Count < 6 ? $"São {ranking.Count} times nas Séries A e B de {liga.Temporada}: a fase de liga precisa de pelo menos 6."
            : ranking.Count % 2 != 0 ? $"São {ranking.Count} times: a fase de liga precisa de um número par, para os dois potes ficarem iguais."
            : null;

        return new CopaFaseDeLigaSorteioDto(
            ligaId, (liga.Temporada ?? 0) - 1, jaSorteada, impedimento is null, impedimento, ranking, jogos);
    }

    public async Task<CopaFaseDeLigaSorteioDto> SortearFaseDeLigaAsync(Guid ligaId, CancellationToken ct)
    {
        var atual = await GetSorteioFaseDeLigaAsync(ligaId, ct)
            ?? throw new InvalidOperationException("Esta competição não é uma Copa em fase de liga.");
        if (!atual.PodeSortear)
            throw new InvalidOperationException(atual.Impedimento ?? "Não dá para sortear agora.");

        var liga = await _db.Ligas.FirstAsync(x => x.LigaId == ligaId, ct);
        var ranking = atual.Ranking.OrderBy(r => r.Ranking).Select(r => r.TimeId).ToList();
        var sorteio = SorteioFaseDeLiga.Sortear(ranking);
        var datas = await DatasDoCalendarioAsync(liga, ct);

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // Sorteio de novo: sai tudo o que o anterior gravou.
            _db.LigaRodadas.RemoveRange(await _db.LigaRodadas.Where(r => r.LigaId == ligaId).ToListAsync(ct));
            _db.LigaGruposTimes.RemoveRange(await _db.LigaGruposTimes.Where(g => g.LigaId == ligaId).ToListAsync(ct));
            _db.LigaCopaPotes.RemoveRange(await _db.LigaCopaPotes.Where(p => p.LigaId == ligaId).ToListAsync(ct));
            await _db.SaveChangesAsync(ct);

            var posicaoNoRanking = atual.Ranking.ToDictionary(r => r.TimeId, r => r.Ranking);
            foreach (var (timeId, pote) in sorteio.Pote1.Select(t => (t, 1)).Concat(sorteio.Pote2.Select(t => (t, 2))))
            {
                _db.LigaCopaPotes.Add(new LigaCopaPote
                {
                    Id = Guid.NewGuid(), LigaId = ligaId, TimeId = timeId, Pote = pote, Ranking = posicaoNoRanking[timeId]
                });
                _db.LigaGruposTimes.Add(new LigaGrupoTime { Id = Guid.NewGuid(), LigaId = ligaId, TimeId = timeId, Grupo = GrupoDaFaseDeLiga });
            }

            foreach (var rodada in sorteio.Jogos.GroupBy(j => j.Rodada).OrderBy(g => g.Key))
            {
                var nova = new LigaRodada
                {
                    RodadaId = Guid.NewGuid(),
                    LigaId = ligaId,
                    Numero = rodada.Key,
                    DataHora = rodada.Key - 1 < datas.Count ? datas[rodada.Key - 1] : null
                };
                foreach (var jogo in rodada)
                    nova.Partidas.Add(new LigaPartida
                    {
                        PartidaId = Guid.NewGuid(),
                        TimeCasaId = jogo.CasaId,
                        TimeForaId = jogo.ForaId,
                        Status = PartidaStatus.Agendada
                    });
                _db.LigaRodadas.Add(nova);
            }

            liga.TotalRodadas = SorteioFaseDeLiga.JogosPorTime;
            liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        return (await GetSorteioFaseDeLigaAsync(ligaId, ct))!;
    }

    /// <summary>
    /// Ranking do sorteio: os times das Séries A e B da temporada da Copa, pela posição geral na
    /// temporada anterior (Série A antes da B) menos o bônus da Copa anterior. Empate: melhor na liga
    /// fica na frente. Quem não jogou a temporada anterior vai para o fim.
    /// </summary>
    private async Task<List<CopaRankingTimeDto>> RankingDaFaseDeLigaAsync(Liga copa, CancellationToken ct)
    {
        if (copa.Temporada is not int temporada) return new List<CopaRankingTimeDto>();

        var times = await _db.LigaTimes.AsNoTracking()
            .Where(t => t.Liga.Temporada == temporada && t.Liga.Tipo == TipoCompetition.Liga)
            .Select(t => new { t.TimeId, t.Time.TeamName })
            .Distinct()
            .ToListAsync(ct);

        var anteriores = await _db.Ligas.AsNoTracking().Where(l => l.Temporada == temporada - 1).ToListAsync(ct);

        // Posição geral: a Série A inteira, depois a Série B.
        var geral = new Dictionary<Guid, (int Posicao, string Origem)>();
        var antes = 0;
        foreach (var liga in anteriores.Where(l => l.Tipo == TipoCompetition.Liga).OrderBy(l => l.Divisao ?? Divisao.SerieA))
        {
            var posicoes = await GetPosicoesTabelaAsync(liga.LigaId, ct);
            foreach (var (timeId, posicao) in posicoes.Where(p => p.Value > 0))
                geral.TryAdd(timeId, (antes + posicao, $"{LigaLabels.Competicao(liga.Tipo, liga.Divisao)} {posicao}º"));
            antes += posicoes.Count;
        }

        var fasesNaCopa = new Dictionary<Guid, FasePremiacao>();
        if (anteriores.FirstOrDefault(l => l.Tipo == TipoCompetition.Copa) is { } copaAnterior)
        {
            var jogos = (await _db.LigaKnockoutJogos.AsNoTracking()
                    .Where(k => k.LigaId == copaAnterior.LigaId)
                    .Select(k => new { k.Fase, k.TimeCasaId, k.TimeForaId, k.VencedorId })
                    .ToListAsync(ct))
                .Select(k => new JogoKnockoutInput(k.Fase, k.TimeCasaId, k.TimeForaId, k.VencedorId));
            var participantes = await _db.LigaGruposTimes.AsNoTracking()
                .Where(g => g.LigaId == copaAnterior.LigaId)
                .Select(g => g.TimeId)
                .ToListAsync(ct);
            fasesNaCopa = FasesDoMataMata.Calcular(jogos, copaAnterior.CampeaoTimeId, participantes);
        }

        var ordenados = times
            .Select(t =>
            {
                var naLiga = geral.TryGetValue(t.TimeId, out var g) ? g : ((int Posicao, string Origem)?)null;
                FasePremiacao? fase = fasesNaCopa.TryGetValue(t.TimeId, out var f) ? f : null;
                var bonus = BonusDaCopa(fase);
                var origem = naLiga?.Origem ?? "Sem temporada anterior";
                if (fase is FasePremiacao fasePremiada && bonus > 0) origem += $" · Copa: {NomeDaFaseNaCopa(fasePremiada)}";
                return (t.TimeId, t.TeamName, Posicao: naLiga?.Posicao, Origem: origem, Bonus: bonus,
                        Nota: (naLiga?.Posicao ?? 1000) - bonus);
            })
            .OrderBy(t => t.Nota)
            .ThenBy(t => t.Posicao ?? int.MaxValue)
            .ThenBy(t => t.TeamName)
            .ToList();

        var metade = ordenados.Count / 2;
        return ordenados
            .Select((t, i) => new CopaRankingTimeDto(i + 1, t.TimeId, t.TeamName, t.Posicao, t.Origem, t.Bonus, i < metade ? 1 : 2))
            .ToList();
    }

    /// <summary>
    /// Mata-mata da fase de liga: playoff 5º x 12º, 6º x 11º, 7º x 10º e 8º x 9º (mando do melhor
    /// colocado) e as quartas já com o 1º ao 4º esperando o vencedor do playoff do lado deles.
    /// </summary>
    private static List<LigaKnockoutJogo> ChaveDaFaseDeLiga(Guid ligaId, IReadOnlyList<Guid> tabela)
    {
        if (tabela.Count < 12)
            throw new InvalidOperationException("A fase de liga precisa de pelo menos 12 times para montar o playoff.");

        LigaKnockoutJogo Jogo(FaseKnockout fase, Guid? casa = null, Guid? fora = null) =>
            new() { KnockoutJogoId = Guid.NewGuid(), LigaId = ligaId, Fase = fase, TimeCasaId = casa, TimeForaId = fora };

        Guid P(int posicao) => tabela[posicao - 1];

        return new List<LigaKnockoutJogo>
        {
            Jogo(FaseKnockout.PO1, P(5), P(12)),
            Jogo(FaseKnockout.PO2, P(6), P(11)),
            Jogo(FaseKnockout.PO3, P(7), P(10)),
            Jogo(FaseKnockout.PO4, P(8), P(9)),
            Jogo(FaseKnockout.QF1, P(1)),
            Jogo(FaseKnockout.QF2, P(2)),
            Jogo(FaseKnockout.QF3, P(3)),
            Jogo(FaseKnockout.QF4, P(4)),
            Jogo(FaseKnockout.Semi1),
            Jogo(FaseKnockout.Semi2),
            Jogo(FaseKnockout.Final)
        };
    }

    /// <summary>Para onde vai o vencedor do playoff: as quartas do lado dele, como visitante.</summary>
    private static FaseKnockout? QuartasDoPlayoff(FaseKnockout fase) => fase switch
    {
        FaseKnockout.PO1 => FaseKnockout.QF4,
        FaseKnockout.PO2 => FaseKnockout.QF3,
        FaseKnockout.PO3 => FaseKnockout.QF2,
        FaseKnockout.PO4 => FaseKnockout.QF1,
        _ => null
    };
}
