using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>Jogos anotados à mão nas competições anteriores ao contador de jogos (sem escalação registrada).</summary>
public class JogosAnotadosService : IJogosAnotadosService
{
    private readonly DraftDbContext _db;
    private readonly ILigaPublicService _liga;

    public JogosAnotadosService(DraftDbContext db, ILigaPublicService liga)
    {
        _db = db;
        _liga = liga;
    }

    public async Task<IReadOnlyList<JogosAnotadosResumoDto>> ListarAsync(CancellationToken ct)
    {
        var linhas = await _db.JogosAnotados.AsNoTracking()
            .Select(j => new { j.JogadorId, j.Jogador.Name, j.LigaId, j.Jogos, j.AtualizadoEmUtc })
            .ToListAsync(ct);

        return linhas
            .GroupBy(j => (j.JogadorId, j.Name))
            .Select(g => new JogosAnotadosResumoDto(
                g.Key.JogadorId, g.Key.Name, g.Sum(j => j.Jogos), g.Select(j => j.LigaId).Distinct().Count(),
                g.Max(j => j.AtualizadoEmUtc)))
            .OrderByDescending(r => r.AtualizadoEmUtc)
            .ToList();
    }

    public async Task<JogosAnotadosJogadorDto?> DoJogadorAsync(int playerId, CancellationToken ct)
    {
        var carreira = await _liga.GetCarreiraJogadorAsync(playerId, ct);
        if (carreira is null) return null;

        var ligas = await LigasSemContagemAsync(ct);
        var ligaIds = ligas.Select(l => l.LigaId).ToList();

        var anotados = await _db.JogosAnotados.AsNoTracking()
            .Where(j => j.JogadorId == playerId && ligaIds.Contains(j.LigaId))
            .ToListAsync(ct);

        var participantes = (await _db.LigaPartidas.AsNoTracking()
                .Where(p => ligaIds.Contains(p.Rodada.LigaId))
                .Select(p => new { p.Rodada.LigaId, p.TimeCasaId, p.TimeForaId })
                .ToListAsync(ct))
            .SelectMany(p => new[] { (p.LigaId, TimeId: p.TimeCasaId), (p.LigaId, TimeId: p.TimeForaId) })
            .Distinct()
            .ToList();
        var nomes = await _db.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);
        string Nome(Guid id) => nomes.TryGetValue(id, out var n) ? n : "?";

        var competicoes = ligas
            .Select(l =>
            {
                var daCarreira = carreira.Estatisticas.Competicoes.Where(c => c.LigaId == l.LigaId).ToList();
                var times = daCarreira.Select(c => c.TimeId)
                    .Concat(anotados.Where(a => a.LigaId == l.LigaId).Select(a => a.TimeId))
                    .Distinct();

                var linhas = times
                    .Select(t => new JogosAnotadosLinhaDto(
                        t, Nome(t), anotados.FirstOrDefault(a => a.LigaId == l.LigaId && a.TimeId == t)?.Jogos))
                    .OrderBy(x => x.TimeNome, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new JogosAnotadosCompeticaoDto(
                    l.LigaId, l.Nome, l.Temporada, linhas,
                    participantes.Where(p => p.LigaId == l.LigaId)
                        .Select(p => new JogosAnotadosTimeDto(p.TimeId, Nome(p.TimeId)))
                        .OrderBy(t => t.TimeNome, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    daCarreira.Sum(c => c.Gols),
                    daCarreira.Sum(c => c.Assistencias));
            })
            .ToList();

        return new JogosAnotadosJogadorDto(
            carreira.PlayerId, carreira.Nome, carreira.PositionName, carreira.TimeAtualNome, competicoes);
    }

    public Task SalvarAsync(int playerId, IReadOnlyCollection<JogosAnotadosSalvarDto> linhas, CancellationToken ct) =>
        GravarAsync(linhas.Select(l => (playerId, l.LigaId, l.TimeId, l.Jogos)).ToList(), ct);

    public Task SalvarDoTimeAsync(Guid timeId, IReadOnlyCollection<JogosAnotadosDoTimeSalvarDto> linhas, CancellationToken ct) =>
        GravarAsync(linhas.Select(l => (l.PlayerId, l.LigaId, timeId, l.Jogos)).ToList(), ct);

    public async Task<IReadOnlyList<JogosAnotadosTimeDto>> TimesAsync(CancellationToken ct)
    {
        var ligaIds = (await LigasSemContagemAsync(ct)).Select(l => l.LigaId).ToList();
        var jogos = await JogosDosTimesAsync(ligaIds, ct);
        var ids = jogos.Keys.Select(k => k.TimeId).Distinct().ToList();

        return await _db.Teams.AsNoTracking()
            .Where(t => ids.Contains(t.TeamId))
            .OrderBy(t => t.TeamName)
            .Select(t => new JogosAnotadosTimeDto(t.TeamId, t.TeamName))
            .ToListAsync(ct);
    }

    public async Task<JogosAnotadosTimeGradeDto?> DoTimeAsync(Guid timeId, CancellationToken ct)
    {
        var timeNome = await _db.Teams.AsNoTracking()
            .Where(t => t.TeamId == timeId).Select(t => t.TeamName).FirstOrDefaultAsync(ct);
        if (timeNome is null) return null;

        // Competições sem contador que o time jogou, com o período (primeiro e último jogo encerrado dele ali).
        var ligas = await LigasSemContagemAsync(ct);
        var ligaIds = ligas.Select(l => l.LigaId).ToList();
        var jogosDoTime = await _db.LigaPartidas.AsNoTracking()
            .Where(p => ligaIds.Contains(p.Rodada.LigaId) && p.Status == PartidaStatus.Encerrada && p.EncerradaEm != null
                        && (p.TimeCasaId == timeId || p.TimeForaId == timeId))
            .Select(p => new { p.Rodada.LigaId, EncerradaEm = p.EncerradaEm!.Value })
            .ToListAsync(ct);
        var periodo = jogosDoTime.GroupBy(j => j.LigaId).ToDictionary(g => g.Key, g => (
            Inicio: g.Min(j => j.EncerradaEm), Fim: g.Max(j => j.EncerradaEm), Jogos: g.Count()));

        var colunas = ligas.Where(l => periodo.ContainsKey(l.LigaId))
            .Select(l => new JogosAnotadosColunaDto(l.LigaId, l.Nome, l.Temporada, periodo[l.LigaId].Jogos))
            .ToList();
        var colIds = colunas.Select(c => c.LigaId).ToList();
        if (colunas.Count == 0)
            return new JogosAnotadosTimeGradeDto(timeId, timeNome, colunas, Array.Empty<JogosAnotadosJogadorDoTimeDto>());

        var eventos = await _db.LigaEventos.AsNoTracking()
            .Where(e => e.TimeId == timeId && colIds.Contains(e.Partida.Rodada.LigaId)
                        && (e.Tipo == TipoEvento.Gol || e.Tipo == TipoEvento.CartaoAmarelo || e.Tipo == TipoEvento.CartaoVermelho))
            .Select(e => new { e.Tipo, e.JogadorId, e.AssistenteId, e.Partida.Rodada.LigaId })
            .ToListAsync(ct);
        var anotados = await _db.JogosAnotados.AsNoTracking()
            .Where(j => j.TimeId == timeId && colIds.Contains(j.LigaId))
            .ToListAsync(ct);

        // Quem pode ter passado pelo time: marcou por ele, já foi anotado, entrou ou saiu dele, ou está no elenco.
        var candidatos = eventos.Select(e => e.JogadorId)
            .Concat(eventos.Where(e => e.AssistenteId != null).Select(e => e.AssistenteId!.Value))
            .Concat(anotados.Select(a => a.JogadorId))
            .ToHashSet();
        candidatos.UnionWith(await _db.TransferHistories.AsNoTracking()
            .Where(t => t.FromTeamId == timeId || t.ToTeamId == timeId).Select(t => t.PlayerId).ToListAsync(ct));
        candidatos.UnionWith(await _db.DraftPicks.AsNoTracking()
            .Where(p => p.PlayerId != null && (p.TeamId == timeId || p.FromTeamId == timeId)).Select(p => p.PlayerId!.Value).ToListAsync(ct));
        candidatos.UnionWith(await _db.TeamRosters.AsNoTracking()
            .Where(r => r.TeamId == timeId).Select(r => r.PlayerId).ToListAsync(ct));
        var ids = candidatos.ToList();

        // Em que time cada um estava em cada período: o mesmo cálculo da carreira do jogador.
        var transferencias = await _db.TransferHistories.AsNoTracking()
            .Where(t => ids.Contains(t.PlayerId))
            .Select(t => new { t.PlayerId, t.PerformedAtUtc, t.FromTeamId, t.ToTeamId })
            .ToListAsync(ct);
        var picks = await _db.DraftPicks.AsNoTracking()
            .Where(p => p.PlayerId != null && ids.Contains(p.PlayerId.Value) && p.Draft.Tipo != DraftTipo.Expansao)
            .Select(p => new { PlayerId = p.PlayerId!.Value, Data = p.PickedAtUtc ?? p.Draft.CreatedAtUtc, p.FromTeamId, p.TeamId })
            .ToListAsync(ct);
        var timeAtual = (await _db.TeamRosters.AsNoTracking()
                .Where(r => ids.Contains(r.PlayerId))
                .Select(r => new { r.PlayerId, r.TeamId })
                .ToListAsync(ct))
            .GroupBy(r => r.PlayerId)
            .ToDictionary(g => g.Key, g => (Guid?)g.First().TeamId);
        var jogadores = await _db.Players.AsNoTracking()
            .Where(p => ids.Contains(p.PlayerId))
            .Select(p => new { p.PlayerId, p.Name, p.PositionId, Posicao = p.Position.Name })
            .ToListAsync(ct);

        var linhas = jogadores
            .Select(j =>
            {
                var vinculos = CarreiraJogador.Vinculos(
                        transferencias.Where(t => t.PlayerId == j.PlayerId)
                            .Select(t => new CarreiraMovimentoInput(t.PerformedAtUtc, t.FromTeamId, t.ToTeamId))
                            .Concat(picks.Where(p => p.PlayerId == j.PlayerId)
                                .Select(p => new CarreiraMovimentoInput(p.Data, p.FromTeamId, p.TeamId))),
                        timeAtual.GetValueOrDefault(j.PlayerId))
                    .Where(v => v.TimeId == timeId)
                    .ToList();

                var celulas = colunas
                    .Select(c =>
                    {
                        var (inicio, fim, _) = periodo[c.LigaId];
                        var gols = eventos.Count(e => e.LigaId == c.LigaId && e.Tipo == TipoEvento.Gol && e.JogadorId == j.PlayerId);
                        var assist = eventos.Count(e => e.LigaId == c.LigaId && e.Tipo == TipoEvento.Gol && e.AssistenteId == j.PlayerId);
                        var anotado = anotados.FirstOrDefault(a => a.LigaId == c.LigaId && a.JogadorId == j.PlayerId)?.Jogos;
                        var noPeriodo = vinculos.Any(v => v.De <= fim && v.Ate >= inicio)
                                        || anotado is not null
                                        || eventos.Any(e => e.LigaId == c.LigaId && (e.JogadorId == j.PlayerId || e.AssistenteId == j.PlayerId));
                        return new JogosAnotadosCelulaDto(c.LigaId, noPeriodo, anotado, gols, assist);
                    })
                    .ToList();

                return new JogosAnotadosJogadorDoTimeDto(j.PlayerId, j.Name, j.PositionId, j.Posicao, celulas);
            })
            .Where(l => l.Celulas.Any(c => c.NoPeriodo))
            .OrderBy(l => l.PositionId)
            .ThenBy(l => l.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new JogosAnotadosTimeGradeDto(timeId, timeNome, colunas, linhas);
    }

    /// <summary>Grava (jogador, competição, time, jogos); jogos nulo apaga a anotação.</summary>
    private async Task GravarAsync(IReadOnlyList<(int JogadorId, Guid LigaId, Guid TimeId, int? Jogos)> linhas, CancellationToken ct)
    {
        if (linhas.Any(l => l.Jogos is < 0))
            throw new InvalidOperationException("O número de jogos não pode ser negativo.");

        // Só vale nas competições sem contador: nas outras o número sai das escalações.
        var permitidas = (await LigasSemContagemAsync(ct)).Select(l => l.LigaId).ToList();
        if (linhas.Any(l => !permitidas.Contains(l.LigaId)))
            throw new InvalidOperationException("Essa competição já tem contador de jogos.");

        // Ninguém joga mais do que o próprio time jogou naquela competição.
        var jogosDoTime = await JogosDosTimesAsync(permitidas, ct);
        var nomes = await _db.Ligas.AsNoTracking()
            .Where(l => permitidas.Contains(l.LigaId)).ToDictionaryAsync(l => l.LigaId, l => l.Nome, ct);
        var demais = linhas.FirstOrDefault(l => l.Jogos > jogosDoTime.GetValueOrDefault((l.LigaId, l.TimeId)));
        if (demais.Jogos is int excesso)
        {
            var nomeJogador = await _db.Players.Where(p => p.PlayerId == demais.JogadorId).Select(p => p.Name).FirstOrDefaultAsync(ct);
            throw new InvalidOperationException(
                $"{nomeJogador}: {excesso} jogos no {nomes[demais.LigaId]}, mas o time só jogou " +
                $"{jogosDoTime.GetValueOrDefault((demais.LigaId, demais.TimeId))} ali.");
        }

        var jogadorIds = linhas.Select(l => l.JogadorId).Distinct().ToList();
        var atuais = await _db.JogosAnotados
            .Where(j => jogadorIds.Contains(j.JogadorId))
            .ToListAsync(ct);
        var agora = DateTime.UtcNow;

        foreach (var linha in linhas.DistinctBy(l => (l.JogadorId, l.LigaId, l.TimeId)))
        {
            var atual = atuais.FirstOrDefault(a =>
                a.JogadorId == linha.JogadorId && a.LigaId == linha.LigaId && a.TimeId == linha.TimeId);
            if (linha.Jogos is not int jogos)
            {
                if (atual is not null) _db.JogosAnotados.Remove(atual);
                continue;
            }

            if (atual is null)
            {
                _db.JogosAnotados.Add(new JogosAnotados
                {
                    JogadorId = linha.JogadorId, LigaId = linha.LigaId, TimeId = linha.TimeId, Jogos = jogos, AtualizadoEmUtc = agora
                });
            }
            else if (atual.Jogos != jogos)
            {
                atual.Jogos = jogos;
                atual.AtualizadoEmUtc = agora;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Jogos encerrados de cada time em cada competição.</summary>
    private async Task<Dictionary<(Guid LigaId, Guid TimeId), int>> JogosDosTimesAsync(List<Guid> ligaIds, CancellationToken ct)
    {
        var partidas = await _db.LigaPartidas.AsNoTracking()
            .Where(p => ligaIds.Contains(p.Rodada.LigaId) && p.Status == PartidaStatus.Encerrada)
            .Select(p => new { p.Rodada.LigaId, p.TimeCasaId, p.TimeForaId })
            .ToListAsync(ct);

        return partidas
            .SelectMany(p => new[] { (p.LigaId, TimeId: p.TimeCasaId), (p.LigaId, TimeId: p.TimeForaId) })
            .GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>Competições já jogadas (algum jogo encerrado) sem nenhuma escalação registrada.</summary>
    private async Task<List<(Guid LigaId, string Nome, int? Temporada)>> LigasSemContagemAsync(CancellationToken ct) =>
        (await _db.Ligas.AsNoTracking()
            .Where(l => _db.LigaPartidas.Any(p => p.Rodada.LigaId == l.LigaId && p.Status == PartidaStatus.Encerrada)
                        && !_db.LigaEscalacoes.Any(e => e.Partida.Rodada.LigaId == l.LigaId))
            .OrderBy(l => l.CriadoEm)
            .Select(l => new { l.LigaId, l.Nome, l.Temporada })
            .ToListAsync(ct))
        .Select(l => (l.LigaId, l.Nome, l.Temporada))
        .ToList();
}
