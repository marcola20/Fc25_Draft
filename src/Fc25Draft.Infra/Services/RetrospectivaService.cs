using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Retrospectiva de uma temporada do time: como terminou cada competição, a campanha somada, os destaques
/// e o mercado. O mercado da temporada vai do fim do Brasileirão anterior até o fim do Brasileirão desta
/// (assim a janela de pré-temporada conta para a temporada que ela montou).
/// </summary>
public class RetrospectivaService : IRetrospectivaService
{
    private readonly DraftDbContext _db;
    private readonly ILigaPublicService _ligas;

    public RetrospectivaService(DraftDbContext db, ILigaPublicService ligas)
    {
        _db = db;
        _ligas = ligas;
    }

    public async Task<IReadOnlyList<int>> TemporadasAsync(Guid timeId, CancellationToken ct) =>
        await _db.Ligas.AsNoTracking()
            .Where(l => l.Temporada != null
                        && (_db.LigaClassificacoes.Any(c => c.LigaId == l.LigaId && c.TimeId == timeId)
                            || _db.LigaPartidas.Any(p => p.Rodada.LigaId == l.LigaId && (p.TimeCasaId == timeId || p.TimeForaId == timeId))))
            .Select(l => l.Temporada!.Value)
            .Distinct()
            .OrderByDescending(t => t)
            .ToListAsync(ct);

    public async Task<RetrospectivaDto?> MontarAsync(Guid timeId, int temporada, CancellationToken ct)
    {
        var timeNome = await _db.Teams.AsNoTracking().Where(t => t.TeamId == timeId).Select(t => t.TeamName).FirstOrDefaultAsync(ct);
        if (timeNome is null) return null;

        var ligas = await _db.Ligas.AsNoTracking()
            .Where(l => l.Temporada == temporada)
            .Select(l => new { l.LigaId, l.Nome, l.Tipo, l.Status, l.CampeaoTimeId, l.DataFim, l.ConfrontoFinalTimeAId, l.ConfrontoFinalTimeBId })
            .ToListAsync(ct);
        var ligaIds = ligas.Select(l => l.LigaId).ToList();

        var participou = (await _db.LigaPartidas.AsNoTracking()
                .Where(p => ligaIds.Contains(p.Rodada.LigaId) && (p.TimeCasaId == timeId || p.TimeForaId == timeId))
                .Select(p => p.Rodada.LigaId)
                .Distinct()
                .ToListAsync(ct))
            .Union(await _db.LigaClassificacoes.AsNoTracking()
                .Where(c => ligaIds.Contains(c.LigaId) && c.TimeId == timeId)
                .Select(c => c.LigaId)
                .ToListAsync(ct))
            .ToHashSet();
        if (participou.Count == 0) return null;
        var doTime = ligas.Where(l => participou.Contains(l.LigaId)).OrderBy(l => l.Tipo).ThenBy(l => l.Nome).ToList();

        // ── Competições ──────────────────────────────────────────────────────────────────────────────
        var trajetoria = (await _ligas.GetTrajetoriaTimeAsync(timeId, ct)).Where(t => t.Temporada == temporada).ToList();
        var mataMata = await _db.LigaKnockoutJogos.AsNoTracking()
            .Where(k => participou.Contains(k.LigaId) && (k.TimeCasaId == timeId || k.TimeForaId == timeId))
            .Select(k => new { k.LigaId, k.Fase, k.VencedorId })
            .ToListAsync(ct);

        var competicoes = doTime.Select(l =>
        {
            var encerrada = l.Status == LigaStatus.Encerrada;
            var campeao = l.CampeaoTimeId == timeId;
            string resultado;

            if (l.Tipo == TipoCompetition.Liga && trajetoria.FirstOrDefault(t => t.LigaNome == l.Nome) is { } tr)
            {
                campeao |= tr.Campeao;
                resultado = tr.Campeao
                    ? tr.Zona == ZonaClassificacao.CampeaoComAcesso ? LigaZonas.Rotulo(tr.Zona) : "Campeão"
                    : $"{tr.Posicao}º de {tr.TotalTimes}" + (tr.Zona == ZonaClassificacao.Nenhuma ? "" : $" · {LigaZonas.Rotulo(tr.Zona)}");
            }
            else if (campeao)
            {
                resultado = "Campeão";
            }
            else
            {
                var jogos = mataMata.Where(k => k.LigaId == l.LigaId).ToList();
                var fase = jogos.Count == 0 ? FaseKnockout.None : jogos.Max(k => k.Fase);
                var perdeuAFinal = jogos.Any(k => k.Fase == FaseKnockout.Final && k.VencedorId is Guid v && v != timeId)
                                   || (l.Tipo == TipoCompetition.Supercopa && encerrada && l.CampeaoTimeId is not null);
                resultado = perdeuAFinal ? "Vice-campeão" : fase switch
                {
                    FaseKnockout.Final => "Final",
                    FaseKnockout.Semi1 or FaseKnockout.Semi2 => "Semifinal",
                    FaseKnockout.QF1 or FaseKnockout.QF2 or FaseKnockout.QF3 or FaseKnockout.QF4 => "Quartas de final",
                    FaseKnockout.PlayIn_A or FaseKnockout.PlayIn_B or FaseKnockout.PlayIn_C => "Play-in",
                    _ => l.Tipo == TipoCompetition.Supercopa ? "Final" : "Fase de grupos"
                };
            }

            return new RetrospectivaCompeticaoDto(l.LigaId, l.Nome, l.Tipo, resultado, campeao, !encerrada);
        }).ToList();

        // ── Campanha e jogos marcantes ───────────────────────────────────────────────────────────────
        var nomesLigas = ligas.ToDictionary(l => l.LigaId, l => l.Nome);
        var partidas = (await _db.LigaPartidas.AsNoTracking()
                .Where(p => participou.Contains(p.Rodada.LigaId) && p.Status == PartidaStatus.Encerrada
                            && (p.TimeCasaId == timeId || p.TimeForaId == timeId))
                .Select(p => new
                {
                    p.PartidaId, p.Rodada.LigaId, p.TimeCasaId, Casa = p.TimeCasa.TeamName, Fora = p.TimeFora.TeamName,
                    p.GolsCasa, p.GolsFora, p.IsWO
                })
                .ToListAsync(ct))
            .Select(p =>
            {
                var emCasa = p.TimeCasaId == timeId;
                return new
                {
                    p.IsWO,
                    Jogo = new RetrospectivaJogoDto(p.PartidaId, emCasa ? p.Fora : p.Casa,
                        emCasa ? p.GolsCasa : p.GolsFora, emCasa ? p.GolsFora : p.GolsCasa, emCasa, nomesLigas[p.LigaId])
                };
            })
            .ToList();
        var jogosDoTime = partidas.Select(p => p.Jogo).ToList();

        var maiorVitoria = partidas.Where(p => !p.IsWO && p.Jogo.GolsPro > p.Jogo.GolsContra)
            .OrderByDescending(p => p.Jogo.GolsPro - p.Jogo.GolsContra).ThenByDescending(p => p.Jogo.GolsPro)
            .Select(p => p.Jogo).FirstOrDefault();
        var piorDerrota = partidas.Where(p => !p.IsWO && p.Jogo.GolsPro < p.Jogo.GolsContra)
            .OrderByDescending(p => p.Jogo.GolsContra - p.Jogo.GolsPro).ThenByDescending(p => p.Jogo.GolsContra)
            .Select(p => p.Jogo).FirstOrDefault();

        // ── Destaques do elenco ──────────────────────────────────────────────────────────────────────
        var partidaIds = jogosDoTime.Select(j => j.PartidaId).ToList();
        var gols = await _db.LigaEventos.AsNoTracking()
            .Where(e => partidaIds.Contains(e.PartidaId) && e.TimeId == timeId && e.Tipo == TipoEvento.Gol)
            .Select(e => new { e.JogadorId, Nome = e.Jogador.Name, e.AssistenteId, AssistenteNome = e.Assistente != null ? e.Assistente.Name : null })
            .ToListAsync(ct);

        var artilheiro = gols.GroupBy(g => (g.JogadorId, g.Nome))
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Nome)
            .Select(g => new RetrospectivaJogadorDto(g.Key.JogadorId, g.Key.Nome, g.Count()))
            .FirstOrDefault();
        var garcom = gols.Where(g => g.AssistenteId is not null)
            .GroupBy(g => (Id: g.AssistenteId!.Value, Nome: g.AssistenteNome ?? ""))
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Nome)
            .Select(g => new RetrospectivaJogadorDto(g.Key.Id, g.Key.Nome, g.Count()))
            .FirstOrDefault();

        var notas = (await _db.LigaNotasJogadores.AsNoTracking()
                .Where(n => partidaIds.Contains(n.PartidaId) && n.TimeId == timeId)
                .Select(n => new { n.JogadorId, Nome = n.Jogador.Name, n.Nota })
                .ToListAsync(ct))
            .GroupBy(n => (n.JogadorId, n.Nome))
            .Select(g => new RetrospectivaJogadorDto(g.Key.JogadorId, g.Key.Nome, Math.Round(g.Average(n => n.Nota), 2), g.Count()))
            .ToList();
        // Média de quem jogou ao menos metade dos jogos com nota de quem mais jogou (evita a nota de um jogo só).
        // Temporada com notas de menos de 3 jogos (antes da importação do PES) fica sem esse destaque.
        var maisJogos = notas.Count == 0 ? 0 : notas.Max(n => n.Jogos);
        var minimo = Math.Max(1, (int)Math.Ceiling(maisJogos / 2.0));
        var melhorNota = notas.Where(n => maisJogos >= 3 && n.Jogos >= minimo)
            .OrderByDescending(n => n.Valor).ThenByDescending(n => n.Jogos)
            .FirstOrDefault();

        // ── Mercado e técnico ────────────────────────────────────────────────────────────────────────
        var brasileiroes = ligas.Where(l => l.Tipo == TipoCompetition.Liga).ToList();
        var fim = (brasileiroes.Count > 0 ? brasileiroes : ligas).Max(l => l.DataFim).Date.AddDays(1);
        var inicio = await _db.Ligas.AsNoTracking()
            .Where(l => l.Temporada < temporada && l.Tipo == TipoCompetition.Liga)
            .MaxAsync(l => (DateTime?)l.DataFim, ct) is DateTime anterior ? anterior.Date.AddDays(1) : DateTime.MinValue;

        var movimentos = await _db.TransferHistories.AsNoTracking()
            .Where(t => t.Type != TransferType.None && (t.ToTeamId == timeId || t.FromTeamId == timeId)
                        && t.PerformedAtUtc >= inicio && t.PerformedAtUtc < fim)
            .Select(t => new
            {
                t.Type, t.PlayerId, Nome = t.Player.Name, t.FromTeamId, t.ToTeamId, t.Amount,
                De = t.FromTeam != null ? t.FromTeam.TeamName : null,
                Para = t.ToTeam != null ? t.ToTeam.TeamName : null
            })
            .ToListAsync(ct);
        var chegadas = movimentos.Where(m => m.ToTeamId == timeId).ToList();
        var saidas = movimentos.Where(m => m.FromTeamId == timeId).ToList();

        var maiorContratacao = chegadas.Where(m => m.Amount > 0).OrderByDescending(m => m.Amount)
            .Select(m => new RetrospectivaTransferenciaDto(m.PlayerId, m.Nome, m.Amount!.Value,
                m.De ?? (m.Type == TransferType.MarketAuction ? "leilão" : null)))
            .FirstOrDefault();
        var maiorVenda = saidas.Where(m => m.Amount > 0).OrderByDescending(m => m.Amount)
            .Select(m => new RetrospectivaTransferenciaDto(m.PlayerId, m.Nome, m.Amount!.Value,
                m.Para ?? (m.Type == TransferType.QuickSell ? "venda rápida" : null)))
            .FirstOrDefault();

        var tecnicos = await _db.TreinadorPassagens.AsNoTracking()
            .Where(p => p.TimeId == timeId && p.Papel == PapelTreinador.Treinador && p.Desde < fim && (p.Ate == null || p.Ate >= inicio))
            .Join(_db.Treinadores, p => p.TreinadorId, t => t.TreinadorId, (p, t) => new { p.Desde, t.Nome })
            .OrderBy(x => x.Desde)
            .Select(x => x.Nome)
            .ToListAsync(ct);

        return new RetrospectivaDto(
            timeId, timeNome, temporada,
            competicoes.Any(c => c.EmAndamento),
            tecnicos.Count == 0 ? null : string.Join(" → ", tecnicos.Distinct()),
            competicoes,
            jogosDoTime.Count,
            jogosDoTime.Count(j => j.GolsPro > j.GolsContra),
            jogosDoTime.Count(j => j.GolsPro == j.GolsContra),
            jogosDoTime.Count(j => j.GolsPro < j.GolsContra),
            jogosDoTime.Sum(j => j.GolsPro),
            jogosDoTime.Sum(j => j.GolsContra),
            artilheiro, garcom, melhorNota, maiorVitoria, piorDerrota, maiorContratacao, maiorVenda,
            chegadas.Count, saidas.Count,
            chegadas.Sum(m => m.Amount ?? 0), saidas.Sum(m => m.Amount ?? 0));
    }
}
