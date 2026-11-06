using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
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

    public async Task SalvarAsync(int playerId, IReadOnlyCollection<JogosAnotadosSalvarDto> linhas, CancellationToken ct)
    {
        if (linhas.Any(l => l.Jogos is < 0))
            throw new InvalidOperationException("O número de jogos não pode ser negativo.");

        // Só vale nas competições sem contador: nas outras o número sai das escalações.
        var permitidas = (await LigasSemContagemAsync(ct)).Select(l => l.LigaId).ToHashSet();
        if (linhas.Any(l => !permitidas.Contains(l.LigaId)))
            throw new InvalidOperationException("Essa competição já tem contador de jogos.");

        var atuais = await _db.JogosAnotados
            .Where(j => j.JogadorId == playerId)
            .ToListAsync(ct);
        var agora = DateTime.UtcNow;

        foreach (var linha in linhas.DistinctBy(l => (l.LigaId, l.TimeId)))
        {
            var atual = atuais.FirstOrDefault(a => a.LigaId == linha.LigaId && a.TimeId == linha.TimeId);
            if (linha.Jogos is not int jogos)
            {
                if (atual is not null) _db.JogosAnotados.Remove(atual);
                continue;
            }

            if (atual is null)
            {
                _db.JogosAnotados.Add(new JogosAnotados
                {
                    JogadorId = playerId, LigaId = linha.LigaId, TimeId = linha.TimeId, Jogos = jogos, AtualizadoEmUtc = agora
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
