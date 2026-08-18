using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Extensions;
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

    private sealed record GolFlat(
        int JogadorId, string JogadorNome, int? AssistenteId,
        Guid LigaId, string LigaNome, TipoCompetition LigaTipo);

    public async Task<IReadOnlyList<HistoricoArtilheiroDto>> GetHistoricoArtilheirosAsync(CancellationToken ct)
    {
        // Projeta apenas os campos necessários dos gols (evita carregar entidades inteiras).
        var gols = await _db.LigaEventos
            .AsNoTracking()
            .Where(e => e.Tipo == TipoEvento.Gol)
            .Select(e => new GolFlat(
                e.JogadorId,
                e.Jogador.Name,
                e.AssistenteId,
                e.Partida.Rodada.LigaId,
                e.Partida.Rodada.Liga.Nome,
                e.Partida.Rodada.Liga.Tipo))
            .ToListAsync(ct);

        // Assistências pré-computadas em passos únicos (evita O(jogadores × eventos)).
        var assistPorJogador = gols
            .Where(g => g.AssistenteId.HasValue)
            .GroupBy(g => g.AssistenteId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var assistPorJogadorLiga = gols
            .Where(g => g.AssistenteId.HasValue)
            .GroupBy(g => (Jogador: g.AssistenteId!.Value, g.LigaId))
            .ToDictionary(g => g.Key, g => g.Count());

        var resultado = gols
            .GroupBy(g => g.JogadorId)
            .Select(pg =>
            {
                var jogadorId = pg.Key;
                var competicoes = pg
                    .GroupBy(g => (g.LigaId, g.LigaNome, g.LigaTipo))
                    .Select(cg => new ArtilheiroCompetitionDetalheDto(
                        cg.Key.LigaId,
                        cg.Key.LigaNome,
                        cg.Key.LigaTipo,
                        cg.Count(),
                        assistPorJogadorLiga.GetValueOrDefault((jogadorId, cg.Key.LigaId), 0)))
                    .OrderByDescending(x => x.Gols)
                    .ToArray();

                return new HistoricoArtilheiroDto(
                    jogadorId,
                    pg.First().JogadorNome,
                    pg.Count(),
                    assistPorJogador.GetValueOrDefault(jogadorId, 0),
                    competicoes);
            })
            .OrderByDescending(x => x.TotalGols)
            .ThenByDescending(x => x.TotalAssistencias)
            .ToArray();

        return resultado;
    }

    public async Task<HistoricoArtilheiroDto?> GetHistoricoArtilheiroDetalheAsync(int jogadorId, CancellationToken ct)
    {
        var historico = await GetHistoricoArtilheirosAsync(ct);
        return historico.FirstOrDefault(h => h.JogadorId == jogadorId);
    }

    private sealed record EventoTimeFlat(
        TipoEvento Tipo, int JogadorId, string JogadorNome,
        int? AssistenteId, string? AssistenteNome);

    public async Task<TimeHistoricoDto?> GetHistoricoTimeAsync(Guid timeId, CancellationToken ct)
    {
        var timeNome = await _db.Teams
            .AsNoTracking()
            .Where(t => t.TeamId == timeId)
            .Select(t => t.TeamName)
            .FirstOrDefaultAsync(ct);

        if (timeNome is null) return null;

        // O TimeId do evento é o time pelo qual o gol foi marcado, então filtrar
        // por ele já exclui o que o jogador fez em outros times.
        var eventos = await _db.LigaEventos
            .AsNoTracking()
            .Where(e => e.TimeId == timeId && e.Tipo == TipoEvento.Gol)
            .Select(e => new EventoTimeFlat(
                e.Tipo,
                e.JogadorId,
                e.Jogador.Name,
                e.AssistenteId,
                e.Assistente!.Name))
            .ToListAsync(ct);

        var golsPorJogador = eventos
            .GroupBy(e => e.JogadorId)
            .ToDictionary(g => g.Key, g => g.Count());

        var assistPorJogador = eventos
            .Where(e => e.AssistenteId.HasValue)
            .GroupBy(e => e.AssistenteId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        // Nomes vêm do próprio evento — assim quem já saiu do time continua aparecendo.
        var nomes = new Dictionary<int, string>();
        foreach (var e in eventos)
        {
            nomes[e.JogadorId] = e.JogadorNome;
            if (e.AssistenteId.HasValue && e.AssistenteNome is not null)
                nomes[e.AssistenteId.Value] = e.AssistenteNome;
        }

        var elencoAtual = await _db.TeamRosters
            .AsNoTracking()
            .Where(r => r.TeamId == timeId)
            .Select(r => r.PlayerId)
            .ToListAsync(ct);

        var elencoSet = elencoAtual.ToHashSet();

        var jogadores = nomes.Keys
            .Select(id => new TimeHistoricoJogadorDto(
                id,
                nomes[id],
                elencoSet.Contains(id),
                golsPorJogador.GetValueOrDefault(id, 0),
                assistPorJogador.GetValueOrDefault(id, 0)))
            .OrderByDescending(j => j.Gols + j.Assistencias)
            .ThenByDescending(j => j.Gols)
            .ThenBy(j => j.JogadorNome)
            .ToArray();

        return new TimeHistoricoDto(
            timeId,
            timeNome,
            jogadores.Sum(j => j.Gols),
            jogadores.Sum(j => j.Assistencias),
            jogadores);
    }

    private static readonly short[] PosicoesDefensivas =
    {
        (short)PositionType.Goleiro,
        (short)PositionType.Zagueiro,
        (short)PositionType.LateralEsquerdo,
        (short)PositionType.LateralDireito
    };

    public async Task<TimeTemporadaDto?> GetTemporadaTimeAsync(Guid timeId, CancellationToken ct)
    {
        var timeNome = await _db.Teams
            .AsNoTracking()
            .Where(t => t.TeamId == timeId)
            .Select(t => t.TeamName)
            .FirstOrDefaultAsync(ct);

        if (timeNome is null) return null;

        // Temporada = competições ainda não encerradas.
        var ligas = await _db.Ligas
            .AsNoTracking()
            .Where(l => l.Status != LigaStatus.Encerrada)
            .OrderBy(l => l.Tipo)
            .ThenByDescending(l => l.CriadoEm)
            .ToListAsync(ct);

        var ligaIds = ligas.Select(l => l.LigaId).ToArray();

        // Traz a classificação inteira das ligas ativas: a posição na Copa é
        // relativa ao grupo, então precisamos dos adversários para calculá-la.
        var classifs = await _db.LigaClassificacoes
            .AsNoTracking()
            .Where(c => ligaIds.Contains(c.LigaId))
            .Select(c => new
            {
                c.LigaId, c.TimeId, c.Posicao, c.Grupo, c.Pontos, c.Jogos,
                c.Vitorias, c.Empates, c.Derrotas, c.GolsPro, c.GolsContra
            })
            .ToListAsync(ct);

        var knockouts = await _db.LigaKnockoutJogos
            .AsNoTracking()
            .Where(k => ligaIds.Contains(k.LigaId)
                        && (k.TimeCasaId == timeId || k.TimeForaId == timeId))
            .Select(k => new { k.LigaId, k.Fase, k.VencedorId })
            .ToListAsync(ct);

        var competicoes = new List<TimeTemporadaCompeticaoDto>();
        foreach (var liga in ligas)
        {
            var c = classifs.FirstOrDefault(x => x.LigaId == liga.LigaId && x.TimeId == timeId);
            if (c is null) continue;

            int? posicao = c.Posicao;
            if (liga.Tipo == TipoCompetition.Copa && c.Grupo is not null)
            {
                // Reordena dentro do grupo para não exibir a posição geral da competição.
                posicao = classifs
                    .Where(x => x.LigaId == liga.LigaId && x.Grupo == c.Grupo)
                    .OrderBy(x => x.Posicao)
                    .Select((x, i) => new { x.TimeId, Pos = i + 1 })
                    .First(x => x.TimeId == timeId).Pos;
            }

            competicoes.Add(new TimeTemporadaCompeticaoDto(
                liga.LigaId, liga.Nome, liga.Tipo, liga.Status,
                posicao, c.Grupo, c.Pontos, c.Jogos,
                c.Vitorias, c.Empates, c.Derrotas,
                c.GolsPro, c.GolsContra, c.GolsPro - c.GolsContra,
                FaseAlcancadaLabel(knockouts
                    .Where(k => k.LigaId == liga.LigaId)
                    .Select(k => (k.Fase, k.VencedorId)), timeId)));
        }

        // Clean sheet é apurado por partida encerrada do time nas competições ativas.
        var partidas = await _db.LigaPartidas
            .AsNoTracking()
            .Where(p => ligaIds.Contains(p.Rodada.LigaId)
                        && p.Status == PartidaStatus.Encerrada
                        && (p.TimeCasaId == timeId || p.TimeForaId == timeId))
            .Select(p => new { p.TimeCasaId, p.GolsCasa, p.GolsFora })
            .ToListAsync(ct);

        var cleanSheets = partidas.Count(p => (p.TimeCasaId == timeId ? p.GolsFora : p.GolsCasa) == 0);

        var eventos = await _db.LigaEventos
            .AsNoTracking()
            .Where(e => e.TimeId == timeId && ligaIds.Contains(e.Partida.Rodada.LigaId))
            .Select(e => new { e.Tipo, e.JogadorId, e.AssistenteId })
            .ToListAsync(ct);

        var elenco = await _db.TeamRosters
            .AsNoTracking()
            .Where(r => r.TeamId == timeId)
            .Select(r => new { r.PlayerId, r.Player.Name, r.Player.PositionId })
            .ToListAsync(ct);

        var jogadores = elenco
            .Select(p => new TimeTemporadaJogadorDto(
                p.PlayerId,
                p.Name,
                p.PositionId,
                ((int)p.PositionId).ToPositionName(),
                PosicoesDefensivas.Contains(p.PositionId) ? cleanSheets : null,
                eventos.Count(e => e.Tipo == TipoEvento.Gol && e.JogadorId == p.PlayerId),
                eventos.Count(e => e.Tipo == TipoEvento.Gol && e.AssistenteId == p.PlayerId),
                eventos.Count(e => e.Tipo == TipoEvento.CartaoAmarelo && e.JogadorId == p.PlayerId),
                eventos.Count(e => e.Tipo == TipoEvento.CartaoVermelho && e.JogadorId == p.PlayerId)))
            .OrderBy(p => p.PositionId)
            .ThenBy(p => p.JogadorNome, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new TimeTemporadaDto(
            timeId,
            timeNome,
            competicoes,
            jogadores,
            competicoes.Sum(x => x.Vitorias),
            competicoes.Sum(x => x.Empates),
            competicoes.Sum(x => x.Derrotas),
            competicoes.Sum(x => x.GolsPro),
            competicoes.Sum(x => x.GolsContra),
            partidas.Count,
            cleanSheets);
    }

    /// <summary>Traduz a fase mais avançada que o time alcançou no mata-mata.</summary>
    private static string? FaseAlcancadaLabel(IEnumerable<(FaseKnockout Fase, Guid? VencedorId)> jogos, Guid timeId)
    {
        var maisAvancado = jogos
            .OrderByDescending(j => j.Fase)
            .Select(j => (Fase: j.Fase, VencedorId: j.VencedorId))
            .FirstOrDefault();

        if (maisAvancado.Fase == FaseKnockout.None) return null;

        var label = FaseLabelMap.GetValueOrDefault(maisAvancado.Fase, maisAvancado.Fase.ToString());

        if (maisAvancado.VencedorId is null)
            return $"Disputando: {label}";

        if (maisAvancado.Fase == FaseKnockout.Final)
            return maisAvancado.VencedorId == timeId ? "Campeão" : "Vice-campeão";

        return maisAvancado.VencedorId == timeId
            ? $"Classificado — {label}"
            : $"Eliminado — {label}";
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
