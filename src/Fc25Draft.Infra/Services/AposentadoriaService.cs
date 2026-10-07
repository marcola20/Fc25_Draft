using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Aposentadoria com aviso. O anúncio grava <see cref="Player.UltimaTemporada"/>; na virada seguinte o jogador sai
/// do elenco (a escalação se limpa sozinha ao salvar) e fica marcado em <see cref="Player.AposentadoNaTemporada"/>.
/// </summary>
public class AposentadoriaService : IAposentadoriaService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public AposentadoriaService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<AposentadoriaPainelDto> GetPainelAsync(int temporada, CancellationToken ct)
    {
        // A idade que vale é a da temporada: se a próxima já fez aniversário, desconta.
        var aniversariosDepois = await _db.EnvelhecimentosTemporada.AsNoTracking().CountAsync(e => e.Temporada > temporada, ct);

        var jogadores = (await _db.Players.AsNoTracking()
                .Where(p => p.Age != null || p.AposentadoNaTemporada != null)
                .Select(p => new
                {
                    p.PlayerId, p.Name, p.Age, p.Overall, p.PositionId, Posicao = p.Position.Name,
                    PosicaoPes = p.Atributos != null ? p.Atributos.PosicaoPes : null,
                    TimeNome = p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault(),
                    p.UltimaTemporada, p.AposentadoNaTemporada
                })
                .ToListAsync(ct))
            .Select(p =>
            {
                var idade = (p.Age ?? 0) - aniversariosDepois;
                var goleiro = p.PosicaoPes is int pp ? pp == 0 : p.PositionId == (short)PositionType.Goleiro;
                var chance = p.AposentadoNaTemporada is null ? Aposentadoria.Chance(idade, goleiro, p.Overall) : 0;
                return new AposentadoriaJogadorDto(p.PlayerId, p.Name, p.TimeNome, p.Posicao, idade, p.Overall, Math.Round(chance, 3),
                    Aposentadoria.Sorteado(temporada, p.PlayerId, chance), p.UltimaTemporada, p.AposentadoNaTemporada);
            })
            .ToList();

        var ativos = jogadores.Where(j => j.AposentadoNaTemporada is null).ToList();

        return new AposentadoriaPainelDto(
            temporada,
            ativos.Where(j => j.UltimaTemporada < temporada).OrderByDescending(j => j.Idade).ToArray(),
            ativos.Where(j => j.UltimaTemporada == temporada).OrderByDescending(j => j.Idade).ToArray(),
            // Todos os ativos que ainda não anunciaram: o sorteio sugere, mas o admin pode escolher qualquer um.
            ativos.Where(j => j.UltimaTemporada is null)
                .OrderByDescending(j => j.Sorteado).ThenByDescending(j => j.Chance).ThenByDescending(j => j.Idade).ToArray(),
            jogadores.Where(j => j.AposentadoNaTemporada is not null)
                .OrderByDescending(j => j.AposentadoNaTemporada).ThenBy(j => j.Nome).ToArray());
    }

    public async Task AnunciarAsync(int temporada, IReadOnlyCollection<int> playerIds, CancellationToken ct)
    {
        var ids = playerIds.Distinct().ToList();
        var jogadores = await _db.Players.Where(p => ids.Contains(p.PlayerId)).ToListAsync(ct);

        var aposentados = jogadores.Where(p => p.AposentadoNaTemporada is not null).Select(p => p.Name).ToList();
        if (aposentados.Count > 0)
            throw new InvalidOperationException($"Já aposentados: {string.Join(", ", aposentados)}.");

        var agora = _time.GetUtcNow().UtcDateTime;
        foreach (var p in jogadores.Where(p => p.UltimaTemporada != temporada))
        {
            p.UltimaTemporada = temporada;
            p.DespedidaAnunciadaEm = agora;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task CancelarAnuncioAsync(int playerId, CancellationToken ct)
    {
        var p = await _db.Players.FirstOrDefaultAsync(x => x.PlayerId == playerId, ct)
            ?? throw new InvalidOperationException("Jogador não encontrado.");
        if (p.AposentadoNaTemporada is not null)
            throw new InvalidOperationException($"{p.Name} já se aposentou: use desfazer a aposentadoria.");

        p.UltimaTemporada = null;
        p.DespedidaAnunciadaEm = null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> AposentarAsync(int temporada, CancellationToken ct)
    {
        var quantos = 0;
        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transacao = await _db.Database.BeginTransactionAsync(ct);

            var jogadores = await _db.Players
                .Where(p => p.AposentadoNaTemporada == null && p.UltimaTemporada != null && p.UltimaTemporada < temporada)
                .ToListAsync(ct);
            var ids = jogadores.Select(p => p.PlayerId).ToList();
            var vinculos = await _db.TeamRosters.Where(r => ids.Contains(r.PlayerId)).ToListAsync(ct);
            var agora = _time.GetUtcNow().UtcDateTime;

            foreach (var p in jogadores)
            {
                var vinculo = vinculos.FirstOrDefault(r => r.PlayerId == p.PlayerId);
                p.TimeAoSeAposentar = vinculo?.TeamId ?? p.CurrentTeamId;
                p.AposentadoNaTemporada = p.UltimaTemporada;
                p.AposentadoEm = agora;
                p.CurrentTeamId = null;

                // Sai do elenco: a escalação se limpa sozinha no SaveChanges (EscalacoesDeQuemSaiu).
                foreach (var r in vinculos.Where(r => r.PlayerId == p.PlayerId))
                    _db.TeamRosters.Remove(r);

                _db.TransferHistories.Add(new TransferHistory
                {
                    TransferId = Guid.NewGuid(),
                    Type = TransferType.Aposentadoria,
                    PlayerId = p.PlayerId,
                    FromTeamId = p.TimeAoSeAposentar,
                    ToTeamId = null,
                    Notes = $"{p.Name} se aposentou depois da temporada {p.AposentadoNaTemporada}.",
                    PerformedBy = "Aposentadoria",
                    PerformedAtUtc = agora,
                    OldOverall = p.Overall,
                    NewOverall = p.Overall
                });
            }

            await _db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
            quantos = jogadores.Count;
        });
        return quantos;
    }

    public async Task DesaposentarAsync(int playerId, CancellationToken ct)
    {
        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transacao = await _db.Database.BeginTransactionAsync(ct);

            var p = await _db.Players.FirstOrDefaultAsync(x => x.PlayerId == playerId, ct)
                ?? throw new InvalidOperationException("Jogador não encontrado.");
            if (p.AposentadoNaTemporada is null)
                throw new InvalidOperationException($"{p.Name} não está aposentado.");

            // Volta para o clube, se ele ainda existir e o jogador não estiver em outro elenco.
            if (p.TimeAoSeAposentar is Guid time
                && await _db.Teams.AnyAsync(t => t.TeamId == time, ct)
                && !await _db.TeamRosters.AnyAsync(r => r.PlayerId == p.PlayerId, ct))
            {
                _db.TeamRosters.Add(new TeamRoster { TeamId = time, PlayerId = p.PlayerId });
                p.CurrentTeamId = time;
            }

            var historico = await _db.TransferHistories
                .Where(t => t.PlayerId == p.PlayerId && t.Type == TransferType.Aposentadoria)
                .ToListAsync(ct);
            _db.TransferHistories.RemoveRange(historico);

            p.AposentadoNaTemporada = null;
            p.AposentadoEm = null;
            p.UltimaTemporada = null;
            p.DespedidaAnunciadaEm = null;
            p.TimeAoSeAposentar = null;

            await _db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        });
    }

    public async Task<IReadOnlyList<AposentadoPesDto>> ListAposentadosParaPesAsync(CancellationToken ct) =>
        await _db.Players.AsNoTracking()
            .Where(p => p.AposentadoNaTemporada != null)
            .OrderBy(p => p.Name)
            .Select(p => new AposentadoPesDto(p.PlayerId, p.Name, p.Atributos != null ? p.Atributos.PesId : null, p.AposentadoNaTemporada!.Value))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct)
    {
        var jogadores = await _db.Players.AsNoTracking()
            .Where(p => p.DespedidaAnunciadaEm != null || p.AposentadoEm != null)
            .Select(p => new
            {
                p.PlayerId, p.Name, p.Age, p.UltimaTemporada, p.DespedidaAnunciadaEm, p.AposentadoNaTemporada, p.AposentadoEm,
                TimeAtual = p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault(),
                TimeAoSeAposentar = _db.Teams.Where(t => t.TeamId == p.TimeAoSeAposentar).Select(t => t.TeamName).FirstOrDefault()
            })
            .ToListAsync(ct);

        var noticias = new List<PlantaoNoticiaDto>();
        foreach (var p in jogadores)
        {
            var link = $"/players/details/{p.PlayerId}";
            if (p.DespedidaAnunciadaEm is DateTime anunciado && p.UltimaTemporada is int ultima)
            {
                var time = p.TimeAoSeAposentar ?? p.TimeAtual;
                noticias.Add(new PlantaoNoticiaDto(anunciado, PlantaoCategoria.Carreira, "👋",
                    $"{p.Name} anuncia que {ultima} é sua última temporada",
                    time is null ? "A despedida dos gramados está marcada" : $"A despedida será pelo {time}", link, time));
            }
            if (p.AposentadoEm is DateTime aposentado)
                noticias.Add(new PlantaoNoticiaDto(aposentado, PlantaoCategoria.Carreira, "🎖️",
                    $"{p.Name} pendura as chuteiras",
                    p.TimeAoSeAposentar is null ? "Fim de carreira" : $"Encerra a carreira no {p.TimeAoSeAposentar}", link, p.TimeAoSeAposentar));
        }

        return noticias.OrderByDescending(n => n.Data).ToArray();
    }
}
