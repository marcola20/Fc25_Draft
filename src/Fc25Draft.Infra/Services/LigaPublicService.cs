using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class LigaPublicService : ILigaPublicService
{
    private readonly DraftDbContext _db;

    public LigaPublicService(DraftDbContext db) => _db = db;

    public async Task<LigaDto?> GetAtualAsync(CancellationToken ct)
    {
        var liga = await _db.Ligas
            .AsNoTracking()
            .Where(x => x.Status != LigaStatus.Encerrada)
            .OrderByDescending(x => x.CriadoEm)
            .FirstOrDefaultAsync(ct)
            ?? await _db.Ligas
                .AsNoTracking()
                .OrderByDescending(x => x.CriadoEm)
                .FirstOrDefaultAsync(ct);

        return liga is null ? null : ToDto(liga);
    }

    public async Task<LigaDto?> GetByIdAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(x => x.LigaId == ligaId, ct);
        return liga is null ? null : ToDto(liga);
    }

    public async Task<IReadOnlyList<LigaDto>> ListAtivasAsync(CancellationToken ct)
    {
        var ligas = await _db.Ligas
            .AsNoTracking()
            .Where(x => x.Status != LigaStatus.Encerrada)
            .OrderByDescending(x => x.CriadoEm)
            .ToListAsync(ct);

        return ligas.Select(ToDto).ToArray();
    }

    public async Task<IReadOnlyList<LigaClassificacaoItemDto>> GetClassificacaoAsync(Guid ligaId, CancellationToken ct)
    {
        var classifs = await _db.LigaClassificacoes
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .Include(x => x.Time)
            .OrderBy(x => x.Posicao)
            .ThenByDescending(x => x.Pontos)
            .ToListAsync(ct);

        var punicoes = await _db.LigaPunicoes
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .GroupBy(x => x.TimeId)
            .ToDictionaryAsync(g => g.Key, g => g.Sum(p => p.PontosSubtraidos), ct);

        return classifs.Select(c => new LigaClassificacaoItemDto(
            c.Posicao, c.TimeId, c.Time.TeamName,
            c.Pontos, c.Jogos, c.Vitorias, c.Empates, c.Derrotas,
            c.GolsPro, c.GolsContra, c.SaldoGols,
            c.CartoesAmarelos, c.CartoesVermelhos,
            punicoes.GetValueOrDefault(c.TimeId, 0),
            c.Grupo)).ToArray();
    }

    public async Task<IReadOnlyList<LigaArtilheiroDto>> GetArtilheirosAsync(Guid ligaId, CancellationToken ct)
    {
        var eventos = await _db.LigaEventos
            .AsNoTracking()
            .Where(x => x.Partida.Rodada.LigaId == ligaId)
            .Include(x => x.Jogador)
            .Include(x => x.Time)
            .ToListAsync(ct);

        var gols = eventos
            .Where(e => e.Tipo == TipoEvento.Gol)
            .GroupBy(e => e.JogadorId)
            .Select(g =>
            {
                var primeiro = g.First();
                var assistencias = eventos.Count(e => e.Tipo == TipoEvento.Gol && e.AssistenteId == primeiro.JogadorId);
                return new LigaArtilheiroDto(
                    primeiro.JogadorId,
                    primeiro.Jogador.Name,
                    primeiro.TimeId,
                    primeiro.Time.TeamName,
                    g.Count(),
                    assistencias);
            })
            .OrderByDescending(x => x.Gols)
            .ThenByDescending(x => x.Assistencias)
            .ToArray();

        return gols;
    }

    public async Task<IReadOnlyList<LigaArtilheiroDto>> GetAssistenciasAsync(Guid ligaId, CancellationToken ct)
    {
        var eventos = await _db.LigaEventos
            .AsNoTracking()
            .Where(x => x.Tipo == TipoEvento.Gol && x.AssistenteId != null
                        && x.Partida.Rodada.LigaId == ligaId)
            .Include(x => x.Assistente)
                .ThenInclude(a => a!.TeamRosters)
                    .ThenInclude(r => r.Team)
            .ToListAsync(ct);

        return eventos
            .GroupBy(e => e.AssistenteId!.Value)
            .Select(g =>
            {
                var player = g.First().Assistente!;
                var roster = player.TeamRosters.FirstOrDefault();
                return new LigaArtilheiroDto(
                    player.PlayerId,
                    player.Name,
                    roster?.TeamId ?? Guid.Empty,
                    roster?.Team?.TeamName ?? "—",
                    0,
                    g.Count());
            })
            .OrderByDescending(x => x.Assistencias)
            .ThenBy(x => x.JogadorNome)
            .ToArray();
    }

    public async Task<IReadOnlyList<LigaCartaoEstatDto>> GetCartoesEstatAsync(Guid ligaId, CancellationToken ct)
    {
        var eventos = await _db.LigaEventos
            .AsNoTracking()
            .Where(x => (x.Tipo == TipoEvento.CartaoAmarelo || x.Tipo == TipoEvento.CartaoVermelho)
                        && x.Partida.Rodada.LigaId == ligaId)
            .Include(x => x.Jogador)
            .Include(x => x.Time)
            .ToListAsync(ct);

        return eventos
            .GroupBy(e => e.JogadorId)
            .Select(g =>
            {
                var primeiro = g.First();
                return new LigaCartaoEstatDto(
                    primeiro.JogadorId,
                    primeiro.Jogador.Name,
                    primeiro.TimeId,
                    primeiro.Time.TeamName,
                    g.Count(e => e.Tipo == TipoEvento.CartaoAmarelo),
                    g.Count(e => e.Tipo == TipoEvento.CartaoVermelho));
            })
            .OrderByDescending(x => x.CartoesVermelhos)
            .ThenByDescending(x => x.CartoesAmarelos)
            .ToArray();
    }

    public async Task<IReadOnlyList<LigaKnockoutJogoDto>> GetKnockoutAsync(Guid ligaId, CancellationToken ct)
    {
        var jogos = await _db.LigaKnockoutJogos
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .Include(x => x.TimeCasa)
            .Include(x => x.TimeFora)
            .Include(x => x.Vencedor)
            .Include(x => x.Partida)
            .OrderBy(x => x.Fase)
            .ToListAsync(ct);

        return jogos.Select(ToKnockoutJogoDto).ToArray();
    }

    public async Task<IReadOnlyList<LigaGrupoTimeDto>> GetGruposAsync(Guid ligaId, CancellationToken ct)
    {
        var grupos = await _db.LigaGruposTimes
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .Include(x => x.Time)
            .OrderBy(x => x.Grupo)
            .ThenBy(x => x.Time.TeamName)
            .ToListAsync(ct);

        return grupos.Select(g => new LigaGrupoTimeDto(g.LigaId, g.TimeId, g.Time.TeamName, g.Grupo)).ToArray();
    }

    public async Task<IReadOnlyList<LigaRodadaComPartidasDto>> GetRodadasComPartidasAsync(Guid ligaId, CancellationToken ct)
    {
        var rodadas = await _db.LigaRodadas
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId && x.Numero > 0)
            .Include(x => x.Partidas).ThenInclude(p => p.TimeCasa)
            .Include(x => x.Partidas).ThenInclude(p => p.TimeFora)
            .OrderBy(x => x.Numero)
            .ToListAsync(ct);

        return rodadas.Select(r => new LigaRodadaComPartidasDto(
            r.RodadaId, r.LigaId, r.Numero,
            r.Partidas.Select(p => new LigaPartidaDto(
                p.PartidaId, p.RodadaId, r.Numero,
                p.TimeCasaId, p.TimeCasa?.TeamName ?? "?",
                p.TimeForaId, p.TimeFora?.TeamName ?? "?",
                p.GolsCasa, p.GolsFora, p.Status, p.IsWO,
                p.TemPenaltis, p.PenaltisVencedorId, p.IniciadaEm, p.EncerradaEm)).ToArray()
        )).ToArray();
    }

    public async Task<IReadOnlyList<LigaEventoDto>> GetEventosPartidaAsync(Guid partidaId, CancellationToken ct)
    {
        var eventos = await _db.LigaEventos
            .AsNoTracking()
            .Where(x => x.PartidaId == partidaId)
            .Include(x => x.Jogador)
            .Include(x => x.Time)
            .Include(x => x.Assistente)
            .OrderBy(x => x.Minuto)
            .ToListAsync(ct);

        return eventos.Select(e => new LigaEventoDto(
            e.EventoId,
            e.PartidaId,
            e.Tipo,
            e.TimeId,
            e.Time?.TeamName ?? "?",
            e.JogadorId,
            e.Jogador?.Name ?? "?",
            e.AssistenteId,
            e.Assistente?.Name,
            e.Minuto,
            e.CriadoEm)).ToArray();
    }

    public async Task<IReadOnlyList<HistoricoArtilheiroDto>> GetHistoricoArtilheirosAsync(CancellationToken ct)
    {
        var ligas = await _db.Ligas.AsNoTracking().ToListAsync(ct);
        var eventos = await _db.LigaEventos
            .AsNoTracking()
            .Include(x => x.Jogador)
            .Include(x => x.Partida)
            .ThenInclude(x => x.Rodada)
            .ThenInclude(x => x.Liga)
            .ToListAsync(ct);

        var golsPorJogador = eventos
            .Where(e => e.Tipo == TipoEvento.Gol)
            .GroupBy(e => e.JogadorId)
            .Select(g =>
            {
                var primeiro = g.First();
                var totalGols = g.Count();
                var totalAssistencias = eventos.Count(e => e.Tipo == TipoEvento.Gol && e.AssistenteId == primeiro.JogadorId);

                var competicoes = g
                    .GroupBy(e => new { e.Partida.Rodada.LigaId, e.Partida.Rodada.Liga.Nome, e.Partida.Rodada.Liga.Tipo })
                    .Select(cg =>
                    {
                        var assistenciasNaCompetition = eventos.Count(e =>
                            e.Tipo == TipoEvento.Gol &&
                            e.AssistenteId == primeiro.JogadorId &&
                            e.Partida.Rodada.LigaId == cg.Key.LigaId);

                        return new ArtilheiroCompetitionDetalheDto(
                            cg.Key.LigaId,
                            cg.Key.Nome,
                            cg.Key.Tipo,
                            cg.Count(),
                            assistenciasNaCompetition);
                    })
                    .OrderByDescending(x => x.Gols)
                    .ToArray();

                return new HistoricoArtilheiroDto(
                    primeiro.JogadorId,
                    primeiro.Jogador.Name,
                    totalGols,
                    totalAssistencias,
                    competicoes);
            })
            .OrderByDescending(x => x.TotalGols)
            .ThenByDescending(x => x.TotalAssistencias)
            .ToArray();

        return golsPorJogador;
    }

    public async Task<HistoricoArtilheiroDto?> GetHistoricoArtilheiroDetalheAsync(int jogadorId, CancellationToken ct)
    {
        var historico = await GetHistoricoArtilheirosAsync(ct);
        return historico.FirstOrDefault(h => h.JogadorId == jogadorId);
    }

    private static LigaDto ToDto(Liga l) =>
        new(l.LigaId, l.Nome, l.TotalRodadas, l.DataInicio, l.DataFim, l.Status, l.Tipo, l.CriadoEm, l.AtualizadoEm);

    private static LigaKnockoutJogoDto ToKnockoutJogoDto(LigaKnockoutJogo j) =>
        new(j.KnockoutJogoId, j.Fase, FaseLabelMap[j.Fase],
            j.TimeCasaId, j.TimeCasa?.TeamName,
            j.TimeForaId, j.TimeFora?.TeamName,
            j.VencedorId, j.Vencedor?.TeamName,
            j.PartidaId, j.Partida?.GolsCasa, j.Partida?.GolsFora,
            j.Partida is null ? null : (PartidaStatus?)j.Partida.Status,
            j.Partida?.TemPenaltis ?? false);

    private static readonly Dictionary<FaseKnockout, string> FaseLabelMap = new()
    {
        [FaseKnockout.PlayIn_A] = "Play-In Jogo 1 (9º vs 10º)",
        [FaseKnockout.PlayIn_B] = "Play-In Jogo 2 (7º vs 8º)",
        [FaseKnockout.PlayIn_C] = "Play-In Jogo 3 (Decisivo)",
        [FaseKnockout.QF1] = "Quartas - 1º vs Vencedor Play-In 3",
        [FaseKnockout.QF2] = "Quartas - 2º vs Vencedor Play-In 2",
        [FaseKnockout.QF3] = "Quartas - 3º vs 6º",
        [FaseKnockout.QF4] = "Quartas - 4º vs 5º",
        [FaseKnockout.Semi1] = "Semifinal 1",
        [FaseKnockout.Semi2] = "Semifinal 2",
        [FaseKnockout.Final] = "Final"
    };
}
