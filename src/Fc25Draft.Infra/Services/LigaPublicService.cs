using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Extensions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class LigaPublicService : ILigaPublicService
{
    private readonly DraftDbContext _db;

    public LigaPublicService(DraftDbContext db) => _db = db;

    public async Task<LigaDto?> GetAtualAsync(CancellationToken ct)
    {
        var liga = await OrdenarPorRelevancia(_db.Ligas.AsNoTracking().Include(x => x.Campeao).Where(x => x.Status != LigaStatus.Encerrada))
            .FirstOrDefaultAsync(ct)
            ?? await OrdenarPorRelevancia(_db.Ligas.AsNoTracking().Include(x => x.Campeao))
                .FirstOrDefaultAsync(ct);

        return liga is null ? null : ToDto(liga);
    }

    /// <summary>
    /// Temporada mais recente primeiro; dentro dela, Liga antes de Copa e Série A antes da B.
    /// Assim a "liga atual" não depende de qual competição foi cadastrada por último.
    /// </summary>
    private static IQueryable<Liga> OrdenarPorRelevancia(IQueryable<Liga> ligas) =>
        ligas
            .OrderByDescending(x => x.Temporada ?? 0)
            .ThenBy(x => x.Tipo)
            .ThenBy(x => x.Divisao ?? Divisao.SerieA)
            .ThenByDescending(x => x.CriadoEm);

    public async Task<LigaDto?> GetByIdAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().Include(x => x.Campeao).FirstOrDefaultAsync(x => x.LigaId == ligaId, ct);
        return liga is null ? null : ToDto(liga);
    }

    public async Task<IReadOnlyList<LigaDto>> ListAtivasAsync(CancellationToken ct)
    {
        var ligas = await OrdenarPorRelevancia(_db.Ligas.AsNoTracking().Include(x => x.Campeao).Where(x => x.Status != LigaStatus.Encerrada))
            .ToListAsync(ct);

        return ligas.Select(ToDto).ToArray();
    }

    public async Task<IReadOnlyList<LigaEdicaoDto>> ListEdicoesAsync(CancellationToken ct)
    {
        // O formato de cada edição sai dos próprios dados dela (times, grupos, rodadas e fases),
        // porque a competição mudou de formato de uma temporada para outra.
        var ligas = await OrdenarPorRelevancia(_db.Ligas.AsNoTracking())
            .Select(l => new
            {
                l.LigaId,
                l.Nome,
                l.Temporada,
                l.Tipo,
                l.Divisao,
                l.Status,
                l.CampeaoTimeId,
                CampeaoNome = l.Campeao != null ? l.Campeao.TeamName : null,
                l.VagasDiretas,
                l.VagasPlayoff,
                l.DataInicio,
                l.DataFim,
                TimesInscritos = _db.LigaTimes.Count(t => t.LigaId == l.LigaId),
                TimesNaTabela = _db.LigaClassificacoes.Count(c => c.LigaId == l.LigaId),
                TimesNosGrupos = _db.LigaGruposTimes.Count(g => g.LigaId == l.LigaId),
                Grupos = _db.LigaGruposTimes.Where(g => g.LigaId == l.LigaId).Select(g => g.Grupo).Distinct().Count(),
                Rodadas = _db.LigaRodadas.Count(r => r.LigaId == l.LigaId && r.Numero > 0 && !r.Desempate),
                PartidasTotal = _db.LigaPartidas.Count(p => p.Rodada.LigaId == l.LigaId),
                PartidasJogadas = _db.LigaPartidas.Count(p => p.Rodada.LigaId == l.LigaId && p.Status == PartidaStatus.Encerrada)
            })
            .ToListAsync(ct);

        var fasesPorLiga = (await _db.LigaKnockoutJogos
                .AsNoTracking()
                .Select(k => new { k.LigaId, k.Fase })
                .Distinct()
                .ToListAsync(ct))
            .GroupBy(k => k.LigaId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<FaseKnockout>)g.Select(x => x.Fase).OrderBy(f => f).ToArray());

        // Edições antigas foram encerradas sem gravar o campeão: nesses casos ele vem
        // do vencedor da final e, se a edição não teve mata-mata, do 1º da tabela.
        var campeaoDaFinal = (await _db.LigaKnockoutJogos
                .AsNoTracking()
                .Where(k => k.Fase == FaseKnockout.Final && k.VencedorId != null)
                .Select(k => new { k.LigaId, TimeId = k.VencedorId!.Value, Nome = k.Vencedor!.TeamName })
                .ToListAsync(ct))
            .GroupBy(k => k.LigaId)
            .ToDictionary(g => g.Key, g => (g.First().TimeId, g.First().Nome));

        var liderDaTabela = (await _db.LigaClassificacoes
                .AsNoTracking()
                .Where(c => c.Posicao == 1)
                .Select(c => new { c.LigaId, c.TimeId, Nome = c.Time.TeamName })
                .ToListAsync(ct))
            .GroupBy(c => c.LigaId)
            // Vários "1º" na mesma liga = grupos da Copa ou empate no topo: não define campeão.
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => (g.First().TimeId, g.First().Nome));

        (Guid? Id, string? Nome) Campeao(Guid ligaId, Guid? gravadoId, string? gravadoNome, LigaStatus status)
        {
            if (gravadoId is not null) return (gravadoId, gravadoNome);
            if (status != LigaStatus.Encerrada) return (null, null);
            if (campeaoDaFinal.TryGetValue(ligaId, out var daFinal)) return (daFinal.Item1, daFinal.Item2);
            return liderDaTabela.TryGetValue(ligaId, out var lider) ? (lider.Item1, lider.Item2) : (null, null);
        }

        return ligas.Select(l =>
        {
            var campeao = Campeao(l.LigaId, l.CampeaoTimeId, l.CampeaoNome, l.Status);

            return new LigaEdicaoDto(
                l.LigaId,
                l.Nome,
                l.Temporada,
                l.Tipo,
                l.Divisao,
                l.Status,
                campeao.Id,
                campeao.Nome,
                Math.Max(l.TimesInscritos, Math.Max(l.TimesNaTabela, l.TimesNosGrupos)),
                l.Grupos,
                l.Rodadas,
                l.PartidasJogadas,
                l.PartidasTotal,
                fasesPorLiga.GetValueOrDefault(l.LigaId, Array.Empty<FaseKnockout>()),
                l.VagasDiretas ?? 0,
                l.VagasPlayoff ?? 0,
                l.DataInicio,
                l.DataFim);
        }).ToArray();
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

    public async Task<IReadOnlyList<TimeTrajetoriaDto>> GetTrajetoriaTimeAsync(Guid timeId, CancellationToken ct)
    {
        var participacoes = await _db.LigaClassificacoes
            .AsNoTracking()
            .Where(c => c.TimeId == timeId
                        && c.Liga.Tipo == TipoCompetition.Liga
                        && c.Liga.Divisao != null
                        && c.Liga.Temporada != null)
            .Select(c => new
            {
                Temporada = c.Liga.Temporada!.Value,
                Divisao = c.Liga.Divisao!.Value,
                c.Liga.Nome,
                c.Liga.Status,
                c.Liga.VagasDiretas,
                c.Liga.VagasPlayoff,
                Campeao = c.Liga.CampeaoTimeId == timeId,
                c.Posicao,
                TotalTimes = _db.LigaClassificacoes.Count(x => x.LigaId == c.LigaId)
            })
            .OrderBy(c => c.Temporada)
            .ToListAsync(ct);

        var trajetoria = new List<TimeTrajetoriaDto>(participacoes.Count);

        for (int i = 0; i < participacoes.Count; i++)
        {
            var p = participacoes[i];
            var regra = LigaRegraZonas.De(TipoCompetition.Liga, p.Divisao, p.VagasDiretas, p.VagasPlayoff);
            var encerrada = p.Status == LigaStatus.Encerrada;

            // Subiu ou desceu = comparação com a divisão da temporada anterior do próprio time.
            var anterior = i > 0 ? participacoes[i - 1] : null;
            var movimento = anterior is null || anterior.Divisao == p.Divisao
                ? null
                : anterior.Divisao == Divisao.SerieB ? "Promovido" : "Rebaixado";

            trajetoria.Add(new TimeTrajetoriaDto(
                p.Temporada, p.Divisao, p.Nome, p.Posicao, p.TotalTimes, encerrada, p.Campeao,
                encerrada ? LigaZonas.Zona(regra, p.Posicao, p.TotalTimes) : ZonaClassificacao.Nenhuma,
                movimento));
        }

        trajetoria.Reverse();
        return trajetoria;
    }

    private const string SemTimeLabel = "Sem time";

    /// <summary>
    /// Time atual de cada jogador (elenco vigente), e nao o time pelo qual o evento
    /// foi registrado — assim quem foi transferido aparece no clube novo e quem foi
    /// dispensado aparece como "Sem time".
    /// </summary>
    private async Task<Dictionary<int, (Guid TimeId, string TimeNome)>> GetTimesAtuaisAsync(
        IReadOnlyCollection<int> jogadorIds, CancellationToken ct)
    {
        if (jogadorIds.Count == 0) return new Dictionary<int, (Guid, string)>();

        var rosters = await _db.TeamRosters
            .AsNoTracking()
            .Where(r => jogadorIds.Contains(r.PlayerId))
            .Select(r => new { r.PlayerId, r.TeamId, r.Team.TeamName })
            .ToListAsync(ct);

        return rosters
            .GroupBy(r => r.PlayerId)
            .ToDictionary(g => g.Key, g =>
            {
                var r = g.First();
                return (r.TeamId, r.TeamName);
            });
    }

    private static (Guid TimeId, string TimeNome) ResolveTimeAtual(
        IReadOnlyDictionary<int, (Guid TimeId, string TimeNome)> timesAtuais, int jogadorId) =>
        timesAtuais.TryGetValue(jogadorId, out var time) ? time : (Guid.Empty, SemTimeLabel);

    public async Task<IReadOnlyList<LigaArtilheiroDto>> GetArtilheirosAsync(Guid ligaId, CancellationToken ct)
    {
        var eventos = await _db.LigaEventos
            .AsNoTracking()
            .Where(x => x.Partida.Rodada.LigaId == ligaId)
            .Include(x => x.Jogador)
            .ToListAsync(ct);

        var timesAtuais = await GetTimesAtuaisAsync(
            eventos.Where(e => e.Tipo == TipoEvento.Gol).Select(e => e.JogadorId).Distinct().ToArray(), ct);

        var gols = eventos
            .Where(e => e.Tipo == TipoEvento.Gol)
            .GroupBy(e => e.JogadorId)
            .Select(g =>
            {
                var primeiro = g.First();
                var assistencias = eventos.Count(e => e.Tipo == TipoEvento.Gol && e.AssistenteId == primeiro.JogadorId);
                var (timeId, timeNome) = ResolveTimeAtual(timesAtuais, primeiro.JogadorId);
                return new LigaArtilheiroDto(
                    primeiro.JogadorId,
                    primeiro.Jogador.Name,
                    timeId,
                    timeNome,
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
            .ToListAsync(ct);

        var timesAtuais = await GetTimesAtuaisAsync(
            eventos.Select(e => e.AssistenteId!.Value).Distinct().ToArray(), ct);

        return eventos
            .GroupBy(e => e.AssistenteId!.Value)
            .Select(g =>
            {
                var player = g.First().Assistente!;
                var (timeId, timeNome) = ResolveTimeAtual(timesAtuais, player.PlayerId);
                return new LigaArtilheiroDto(
                    player.PlayerId,
                    player.Name,
                    timeId,
                    timeNome,
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
            .ToListAsync(ct);

        var timesAtuais = await GetTimesAtuaisAsync(
            eventos.Select(e => e.JogadorId).Distinct().ToArray(), ct);

        return eventos
            .GroupBy(e => e.JogadorId)
            .Select(g =>
            {
                var primeiro = g.First();
                var (timeId, timeNome) = ResolveTimeAtual(timesAtuais, primeiro.JogadorId);
                return new LigaCartaoEstatDto(
                    primeiro.JogadorId,
                    primeiro.Jogador.Name,
                    timeId,
                    timeNome,
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
                p.TemPenaltis, p.PenaltisVencedorId, p.IniciadaEm, p.EncerradaEm)).ToArray(),
            r.Desempate
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
        int JogadorId, string JogadorNome, int? AssistenteId, string? AssistenteNome,
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
                e.Assistente == null ? null : e.Assistente.Name,
                e.Partida.Rodada.LigaId,
                e.Partida.Rodada.Liga.Nome,
                e.Partida.Rodada.Liga.Tipo))
            .ToListAsync(ct);

        var nomesPorJogador = new Dictionary<int, string>();
        foreach (var g in gols)
        {
            nomesPorJogador[g.JogadorId] = g.JogadorNome;
            if (g.AssistenteId.HasValue && g.AssistenteNome is not null)
                nomesPorJogador[g.AssistenteId.Value] = g.AssistenteNome;
        }

        var ligaInfoPorId = gols
            .Select(g => (g.LigaId, g.LigaNome, g.LigaTipo))
            .Distinct()
            .ToDictionary(x => x.LigaId, x => (x.LigaNome, x.LigaTipo));

        var golsPorJogadorLiga = gols
            .GroupBy(g => (g.JogadorId, g.LigaId))
            .ToDictionary(g => g.Key, g => g.Count());

        var assistPorJogadorLiga = gols
            .Where(g => g.AssistenteId.HasValue)
            .GroupBy(g => (JogadorId: g.AssistenteId!.Value, g.LigaId))
            .ToDictionary(g => g.Key, g => g.Count());

        // União das chaves (jogador, liga) vindas de gols OU assistências, para não
        // perder competições em que o jogador só deu assistência (sem marcar gol).
        var chavesJogadorLiga = golsPorJogadorLiga.Keys.Concat(assistPorJogadorLiga.Keys).Distinct();

        var resultado = chavesJogadorLiga
            .GroupBy(k => k.JogadorId)
            .Select(pg =>
            {
                var jogadorId = pg.Key;
                var competicoes = pg
                    .Select(k =>
                    {
                        var (ligaNome, ligaTipo) = ligaInfoPorId[k.LigaId];
                        return new ArtilheiroCompetitionDetalheDto(
                            k.LigaId,
                            ligaNome,
                            ligaTipo,
                            golsPorJogadorLiga.GetValueOrDefault(k, 0),
                            assistPorJogadorLiga.GetValueOrDefault(k, 0));
                    })
                    .OrderByDescending(x => x.Gols)
                    .ThenByDescending(x => x.Assistencias)
                    .ToArray();

                return new HistoricoArtilheiroDto(
                    jogadorId,
                    nomesPorJogador.GetValueOrDefault(jogadorId, "?"),
                    competicoes.Sum(c => c.Gols),
                    competicoes.Sum(c => c.Assistencias),
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
            .Select(c => new ClassifFlat(
                c.LigaId, c.TimeId, c.Posicao, c.Grupo, c.Pontos, c.Jogos,
                c.Vitorias, c.Empates, c.Derrotas, c.GolsPro, c.GolsContra))
            .ToListAsync(ct);

        var knockouts = await _db.LigaKnockoutJogos
            .AsNoTracking()
            .Where(k => ligaIds.Contains(k.LigaId)
                        && (k.TimeCasaId == timeId || k.TimeForaId == timeId))
            .Select(k => new { k.LigaId, k.Fase, k.VencedorId })
            .ToListAsync(ct);

        // Jogos decisivos encerrados: na Copa, o vencedor fica à frente do empatado.
        var jogosDecisivos = await _db.LigaPartidas
            .AsNoTracking()
            .Where(p => ligaIds.Contains(p.Rodada.LigaId) && p.Rodada.Desempate
                        && p.Status == PartidaStatus.Encerrada)
            .Select(p => new
            {
                p.Rodada.LigaId, p.TimeCasaId, p.TimeForaId,
                p.GolsCasa, p.GolsFora, p.TemPenaltis, p.PenaltisVencedorId
            })
            .ToListAsync(ct);

        var decisivosPorLiga = jogosDecisivos
            .GroupBy(j => j.LigaId)
            .ToDictionary(
                g => g.Key,
                g => new JogosDecisivos(g
                    .Select(j => (Vencedor: LigaDesempate.VencedorDoJogoDecisivo(
                        j.TimeCasaId, j.TimeForaId, j.GolsCasa, j.GolsFora, j.TemPenaltis, j.PenaltisVencedorId),
                        j.TimeCasaId, j.TimeForaId))
                    .Where(x => x.Vencedor is not null)
                    .Select(x => (x.Vencedor!.Value, x.Vencedor == x.TimeCasaId ? x.TimeForaId : x.TimeCasaId))));

        var competicoes = new List<TimeTemporadaCompeticaoDto>();
        foreach (var liga in ligas)
        {
            var c = classifs.FirstOrDefault(x => x.LigaId == liga.LigaId && x.TimeId == timeId);
            if (c is null) continue;

            int? posicao = c.Posicao;
            if (liga.Tipo == TipoCompetition.Copa && c.Grupo is not null)
            {
                // Reordena dentro do grupo para não exibir a posição geral da competição.
                // Empatados em Pts/V/SG dividem a mesma posição (jogo decisivo).
                var decisivos = decisivosPorLiga.GetValueOrDefault(liga.LigaId, JogosDecisivos.Nenhum);

                var doGrupo = LigaDesempate.Ordenar(
                    classifs.Where(x => x.LigaId == liga.LigaId && x.Grupo == c.Grupo),
                    TipoCompetition.Copa,
                    x => x.TimeId,
                    ClassifFlat.Stats,
                    Array.Empty<ConfrontoDireto>(),
                    decisivos);

                var posicoesGrupo = LigaDesempate.PosicoesCopa(
                    doGrupo, x => x.TimeId, ClassifFlat.Stats, decisivos);

                posicao = posicoesGrupo[doGrupo.FindIndex(x => x.TimeId == timeId)];
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
    /// <summary>Linha de classificação achatada, usada no cálculo da posição por grupo.</summary>
    private sealed record ClassifFlat(
        Guid LigaId,
        Guid TimeId,
        int Posicao,
        GrupoCopa? Grupo,
        int Pontos,
        int Jogos,
        int Vitorias,
        int Empates,
        int Derrotas,
        int GolsPro,
        int GolsContra)
    {
        public static DesempateStats Stats(ClassifFlat c) =>
            new(c.Pontos, c.Vitorias, c.GolsPro - c.GolsContra, c.GolsPro);
    }

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
        new(l.LigaId, l.Nome, l.TotalRodadas, l.DataInicio, l.DataFim, l.Status, l.Tipo, l.CriadoEm, l.AtualizadoEm,
            l.CampeaoTimeId, l.Campeao?.TeamName, l.Temporada, l.Divisao, l.VagasDiretas, l.VagasPlayoff);

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
        [FaseKnockout.QF1] = "Quartas 1 — 1º Grupo A x 2º Grupo B",
        [FaseKnockout.QF2] = "Quartas 2 — 1º Grupo B x 2º Grupo A",
        [FaseKnockout.QF3] = "Quartas 3 — 1º Grupo C x 2º Grupo D",
        [FaseKnockout.QF4] = "Quartas 4 — 1º Grupo D x 2º Grupo C",
        [FaseKnockout.Semi1] = "Semifinal 1",
        [FaseKnockout.Semi2] = "Semifinal 2",
        [FaseKnockout.Final] = "Final"
    };
}
