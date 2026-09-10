using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class DraftWishlistService : IDraftWishlistService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public DraftWishlistService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<DraftWishlistEdicaoDto>> GetEdicoesAsync(CancellationToken ct = default)
    {
        await GetOrCreateEdicaoAtualAsync(ct);

        return await _db.DraftWishlistEdicoes
            .AsNoTracking()
            .OrderByDescending(e => e.Numero)
            .Select(e => new DraftWishlistEdicaoDto(
                e.Numero,
                e.Nome,
                e.CriadoEm,
                e.EncerradoEm,
                e.EncerradoEm == null,
                e.Entradas.Select(x => x.TeamId).Distinct().Count()))
            .ToListAsync(ct);
    }

    public async Task<DraftWishlistDto> GetByTokenAsync(string token, int? versao = null, CancellationToken ct = default)
    {
        var team = await ResolveTeamAsync(token, ct);
        var edicao = await ResolveEdicaoAsync(versao, ct);
        return await BuildDtoAsync(edicao, team.TeamId, team.TeamName, ct);
    }

    public async Task<DraftWishlistDto> SaveAsync(string token, IReadOnlyList<int> playerIds, CancellationToken ct = default)
    {
        var team = await ResolveTeamAsync(token, ct);
        var edicao = await GetOrCreateEdicaoAtualAsync(ct);

        if (!edicao.Aberta)
            throw new InvalidOperationException($"Os envios da {edicao.Nome} estão encerrados.");

        if (playerIds is null || playerIds.Count == 0)
            throw new InvalidOperationException("Informe os jogadores da lista.");

        var ids = playerIds.Distinct().ToList();
        if (ids.Count != playerIds.Count)
            throw new InvalidOperationException("A lista contém jogadores repetidos.");

        if (ids.Count != DraftWishlistRules.MaxJogadores)
            throw new InvalidOperationException($"A lista deve conter exatamente {DraftWishlistRules.MaxJogadores} jogadores.");

        var players = await _db.Players
            .AsNoTracking()
            .Where(p => ids.Contains(p.PlayerId))
            .Select(p => new { p.PlayerId, p.Name, p.Overall, Escolhido = p.TeamRosters.Any() })
            .ToListAsync(ct);

        if (players.Count != ids.Count)
            throw new InvalidOperationException("Um ou mais jogadores não foram encontrados.");

        var escolhidos = players.Where(p => p.Escolhido).Select(p => p.Name).ToList();
        if (escolhidos.Count > 0)
            throw new InvalidOperationException($"Jogadores já pertencem a um time: {string.Join(", ", escolhidos)}.");

        var foraDaFaixa = players
            .Where(p => p.Overall < DraftWishlistRules.OverallMinimo || p.Overall > DraftWishlistRules.OverallMaximo)
            .Select(p => $"{p.Name} ({p.Overall})")
            .ToList();
        if (foraDaFaixa.Count > 0)
            throw new InvalidOperationException(
                $"Jogadores fora da faixa de overall do draft ({DraftWishlistRules.OverallMinimo} a {DraftWishlistRules.OverallMaximo}): {string.Join(", ", foraDaFaixa)}.");

        var now = _time.GetUtcNow().UtcDateTime;

        var existentes = await _db.DraftWishlistEntries
            .Where(e => e.Versao == edicao.Numero && e.TeamId == team.TeamId)
            .ToListAsync(ct);
        _db.DraftWishlistEntries.RemoveRange(existentes);

        for (var i = 0; i < ids.Count; i++)
        {
            _db.DraftWishlistEntries.Add(new DraftWishlistEntry
            {
                DraftWishlistEntryId = Guid.NewGuid(),
                Versao = edicao.Numero,
                TeamId = team.TeamId,
                PlayerId = ids[i],
                Ordem = i + 1,
                CriadoEm = now
            });
        }

        await _db.SaveChangesAsync(ct);

        return await BuildDtoAsync(edicao, team.TeamId, team.TeamName, ct);
    }

    public async Task<IReadOnlyList<DraftWishlistDto>> GetAllAsync(int? versao = null, CancellationToken ct = default)
    {
        var edicao = await ResolveEdicaoAsync(versao, ct);

        var rows = await _db.DraftWishlistEntries
            .AsNoTracking()
            .Where(e => e.Versao == edicao.Numero)
            .Select(e => new
            {
                e.TeamId,
                e.Team.TeamName,
                e.CriadoEm,
                Jogador = new DraftWishlistPlayerDto(
                    e.Ordem,
                    e.PlayerId,
                    e.Player.Name,
                    e.Player.PositionId,
                    e.Player.Position.Name,
                    e.Player.Overall,
                    e.Player.Age,
                    !e.Player.TeamRosters.Any())
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => new { r.TeamId, r.TeamName })
            .OrderBy(g => g.Key.TeamName)
            .Select(g => new DraftWishlistDto(
                edicao.Numero,
                edicao.Nome,
                edicao.Aberta,
                g.Key.TeamId,
                g.Key.TeamName,
                g.Max(r => (DateTime?)r.CriadoEm),
                g.Select(r => r.Jogador).OrderBy(j => j.Ordem).ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<DraftWishlistVoteDto>> GetVotesAsync(int? versao = null, CancellationToken ct = default)
    {
        var edicao = await ResolveEdicaoAsync(versao, ct);

        var rows = await _db.DraftWishlistEntries
            .AsNoTracking()
            .Where(e => e.Versao == edicao.Numero)
            .Select(e => new
            {
                e.PlayerId,
                e.Player.Name,
                e.Player.PositionId,
                PositionName = e.Player.Position.Name,
                e.Player.Overall,
                e.Player.Age,
                Disponivel = !e.Player.TeamRosters.Any(),
                e.TeamId,
                e.Team.TeamName,
                e.Ordem
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.PlayerId)
            .Select(g =>
            {
                var first = g.First();
                var times = g
                    .OrderBy(r => r.Ordem)
                    .ThenBy(r => r.TeamName)
                    .Select(r => new DraftWishlistVoteTeamDto(r.TeamId, r.TeamName, r.Ordem))
                    .ToList();

                return new DraftWishlistVoteDto(
                    first.PlayerId,
                    first.Name,
                    first.PositionId,
                    first.PositionName,
                    first.Overall,
                    first.Age,
                    first.Disponivel,
                    times.Count,
                    times.Min(t => t.Ordem),
                    times);
            })
            .OrderByDescending(v => v.Votos)
            .ThenBy(v => v.MelhorPosicao)
            .ThenByDescending(v => v.Overall)
            .ThenBy(v => v.Name)
            .ToList();
    }

    public async Task<DraftWishlistEdicaoDto> AbrirNovaEdicaoAsync(string? nome, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;

        var edicoes = await _db.DraftWishlistEdicoes.ToListAsync(ct);
        foreach (var aberta in edicoes.Where(e => e.Aberta))
            aberta.EncerradoEm = now;

        var numero = edicoes.Count == 0 ? 1 : edicoes.Max(e => e.Numero) + 1;
        var nomeNormalizado = string.IsNullOrWhiteSpace(nome) ? $"Versão {numero}" : nome.Trim();
        if (nomeNormalizado.Length > 80)
            throw new InvalidOperationException("O nome da versão deve ter no máximo 80 caracteres.");

        var nova = new DraftWishlistEdicao
        {
            Numero = numero,
            Nome = nomeNormalizado,
            CriadoEm = now,
            EncerradoEm = null
        };

        _db.DraftWishlistEdicoes.Add(nova);
        await _db.SaveChangesAsync(ct);

        return ToDto(nova, 0);
    }

    public async Task<DraftWishlistEdicaoDto> AlterarStatusEdicaoAsync(int numero, bool aberta, CancellationToken ct = default)
    {
        var edicoes = await _db.DraftWishlistEdicoes.ToListAsync(ct);
        var alvo = edicoes.FirstOrDefault(e => e.Numero == numero)
            ?? throw new InvalidOperationException("Versão não encontrada.");

        var now = _time.GetUtcNow().UtcDateTime;

        if (aberta)
        {
            foreach (var outra in edicoes.Where(e => e.Numero != numero && e.Aberta))
                outra.EncerradoEm = now;
            alvo.EncerradoEm = null;
        }
        else if (alvo.Aberta)
        {
            alvo.EncerradoEm = now;
        }

        await _db.SaveChangesAsync(ct);

        var totalListas = await _db.DraftWishlistEntries
            .Where(e => e.Versao == numero)
            .Select(e => e.TeamId)
            .Distinct()
            .CountAsync(ct);

        return ToDto(alvo, totalListas);
    }

    /// <summary>Versão atual: a aberta, ou a de maior número se nenhuma estiver aberta. Cria a versão 1 se não houver nenhuma.</summary>
    private async Task<DraftWishlistEdicao> GetOrCreateEdicaoAtualAsync(CancellationToken ct)
    {
        var atual = await _db.DraftWishlistEdicoes
            .AsNoTracking()
            .OrderByDescending(e => e.EncerradoEm == null)
            .ThenByDescending(e => e.Numero)
            .FirstOrDefaultAsync(ct);

        if (atual is not null)
            return atual;

        atual = new DraftWishlistEdicao
        {
            Numero = 1,
            Nome = "Versão 1",
            CriadoEm = _time.GetUtcNow().UtcDateTime
        };
        _db.DraftWishlistEdicoes.Add(atual);
        await _db.SaveChangesAsync(ct);
        _db.Entry(atual).State = EntityState.Detached;
        return atual;
    }

    private async Task<DraftWishlistEdicao> ResolveEdicaoAsync(int? versao, CancellationToken ct)
    {
        if (!versao.HasValue)
            return await GetOrCreateEdicaoAtualAsync(ct);

        return await _db.DraftWishlistEdicoes.AsNoTracking().FirstOrDefaultAsync(e => e.Numero == versao.Value, ct)
            ?? throw new InvalidOperationException("Versão não encontrada.");
    }

    private async Task<Team> ResolveTeamAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException("Token do time obrigatório.");

        var normalized = token.Trim();
        var team = await _db.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Token == normalized || t.AuxToken == normalized, ct);

        return team ?? throw new UnauthorizedAccessException("Token do time inválido.");
    }

    private async Task<DraftWishlistDto> BuildDtoAsync(DraftWishlistEdicao edicao, Guid teamId, string teamName, CancellationToken ct)
    {
        var rows = await _db.DraftWishlistEntries
            .AsNoTracking()
            .Where(e => e.Versao == edicao.Numero && e.TeamId == teamId)
            .OrderBy(e => e.Ordem)
            .Select(e => new
            {
                e.CriadoEm,
                Jogador = new DraftWishlistPlayerDto(
                    e.Ordem,
                    e.PlayerId,
                    e.Player.Name,
                    e.Player.PositionId,
                    e.Player.Position.Name,
                    e.Player.Overall,
                    e.Player.Age,
                    !e.Player.TeamRosters.Any())
            })
            .ToListAsync(ct);

        return new DraftWishlistDto(
            edicao.Numero,
            edicao.Nome,
            edicao.Aberta,
            teamId,
            teamName,
            rows.Count == 0 ? null : rows.Max(r => r.CriadoEm),
            rows.Select(r => r.Jogador).ToList());
    }

    private static DraftWishlistEdicaoDto ToDto(DraftWishlistEdicao e, int totalListas) =>
        new(e.Numero, e.Nome, e.CriadoEm, e.EncerradoEm, e.Aberta, totalListas);
}
