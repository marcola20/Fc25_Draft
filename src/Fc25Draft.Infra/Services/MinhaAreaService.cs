using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class MinhaAreaService : IMinhaAreaService
{
    private readonly DraftDbContext _db;

    public MinhaAreaService(DraftDbContext db) => _db = db;

    public async Task<(Guid TeamId, string TeamName)?> ResolverTimeAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var acesso = await _db.AcessoPorTokenAsync(token, ct);

        return acesso is null ? null : (acesso.TimeId, acesso.TimeNome);
    }

    public async Task<MinhaAreaDto> GetAsync(string? token, CancellationToken ct)
    {
        var (teamId, _) = await ResolverTimeAsync(token, ct)
            ?? throw new InvalidOperationException("Token não pertence a nenhum time. A Minha Área é do time: entre com o token do seu time.");

        var time = await _db.Teams.AsNoTracking()
            .Where(t => t.TeamId == teamId)
            .Select(t => new { t.TeamName, t.OwnerName, t.Budget, t.BudgetBlocked, Elenco = t.Roster.Count })
            .FirstAsync(ct);

        var nomes = await _db.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);
        string? Nome(Guid? id) => id is Guid g && nomes.TryGetValue(g, out var n) ? n : null;

        return new MinhaAreaDto(
            teamId,
            time.TeamName,
            time.OwnerName,
            time.Budget,
            time.BudgetBlocked,
            time.Elenco,
            await LeiloesAsync(teamId, Nome, ct),
            await PropostasAsync(teamId, Nome, ct),
            await DraftAsync(teamId, ct),
            await EscolhaAutomaticaAsync(teamId, ct),
            await ObservadosAsync(teamId, Nome, ct));
    }

    public async Task<bool> EstaObservandoAsync(string? token, int playerId, CancellationToken ct)
    {
        var time = await ResolverTimeAsync(token, ct);
        return time is not null
               && await _db.TeamObservacoes.AnyAsync(o => o.TeamId == time.Value.TeamId && o.PlayerId == playerId, ct);
    }

    public async Task<bool> AlternarObservacaoAsync(string? token, int playerId, CancellationToken ct)
    {
        var (teamId, _) = await ResolverTimeAsync(token, ct)
            ?? throw new InvalidOperationException("Entre com o token do seu time para observar jogadores.");

        var removidos = await _db.TeamObservacoes
            .Where(o => o.TeamId == teamId && o.PlayerId == playerId)
            .ExecuteDeleteAsync(ct);
        if (removidos > 0)
            return false;

        if (!await _db.Players.AnyAsync(p => p.PlayerId == playerId, ct))
            throw new InvalidOperationException("Jogador não encontrado.");

        _db.TeamObservacoes.Add(new TeamObservacao { TeamId = teamId, PlayerId = playerId, CriadoEm = DateTime.UtcNow });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ── Partes do painel ─────────────────────────────────────────────────────

    /// <summary>Leilões ainda abertos em que o time deu lance, com quem está na frente.</summary>
    private async Task<IReadOnlyList<MinhaAreaLeilaoDto>> LeiloesAsync(Guid teamId, Func<Guid?, string?> nome, CancellationToken ct)
    {
        var itens = await _db.MarketBids.AsNoTracking()
            .Where(b => b.TeamId == teamId && b.Item.Status == MarketItemStatus.Active)
            .GroupBy(b => b.ItemId)
            .Select(g => new { ItemId = g.Key, MeuMaiorLance = g.Max(b => b.Amount) })
            .ToListAsync(ct);

        var ids = itens.Select(i => i.ItemId).ToList();
        var detalhes = await _db.MarketItems.AsNoTracking()
            .Where(i => ids.Contains(i.ItemId))
            .Select(i => new
            {
                i.ItemId, i.PlayerId, i.Player.Name, Posicao = i.Player.Position.Name, i.Player.Overall,
                Atual = i.CurrentLeaderAmount ?? i.BasePrice, i.CurrentLeaderTeamId, i.ExpiresAtUtc
            })
            .ToListAsync(ct);

        return detalhes
            .Select(d => new MinhaAreaLeilaoDto(d.ItemId, d.PlayerId, d.Name, d.Posicao, d.Overall, d.Atual,
                nome(d.CurrentLeaderTeamId), d.CurrentLeaderTeamId == teamId,
                itens.First(i => i.ItemId == d.ItemId).MeuMaiorLance, d.ExpiresAtUtc))
            // Superados primeiro: são os que pedem ação.
            .OrderBy(l => l.EstouGanhando)
            .ThenBy(l => l.TerminaEm)
            .ToList();
    }

    private async Task<IReadOnlyList<MinhaAreaPropostaDto>> PropostasAsync(Guid teamId, Func<Guid?, string?> nome, CancellationToken ct)
    {
        var ofertas = await _db.TransferOffers.AsNoTracking()
            .Where(o => o.Status == OfferStatus.Pending && (o.ToTeamId == teamId || o.FromTeamId == teamId))
            .Select(o => new
            {
                o.OfferId, o.FromTeamId, o.ToTeamId, o.Type, o.Money, o.CreatedAtUtc,
                Jogadores = o.Players.Select(p => p.Player.Name).ToList()
            })
            .ToListAsync(ct);

        return ofertas
            .Select(o => new MinhaAreaPropostaDto(o.OfferId, o.ToTeamId == teamId,
                nome(o.ToTeamId == teamId ? o.FromTeamId : o.ToTeamId) ?? "?", o.Type, o.Money, o.Jogadores, o.CreatedAtUtc))
            // Recebidas primeiro: esperam a resposta do time.
            .OrderByDescending(o => o.Recebida)
            .ThenByDescending(o => o.CriadaEm)
            .ToList();
    }

    private async Task<MinhaAreaDraftDto?> DraftAsync(Guid teamId, CancellationToken ct)
    {
        var draft = await _db.Drafts.AsNoTracking()
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new { d.DraftId, d.Name })
            .FirstOrDefaultAsync(ct);
        if (draft is null) return null;

        var pendentes = await _db.DraftPicks.AsNoTracking()
            .Where(p => p.DraftId == draft.DraftId && p.PlayerId == null)
            .OrderBy(p => p.OverallPick)
            .Select(p => new { p.TeamId, p.RoundNumber, p.OverallPick })
            .ToListAsync(ct);
        if (pendentes.Count == 0) return null;

        var minhas = pendentes.Where(p => p.TeamId == teamId).ToList();
        if (minhas.Count == 0)
            return new MinhaAreaDraftDto(draft.Name, null, null, null, 0);

        var proxima = minhas[0];
        return new MinhaAreaDraftDto(draft.Name, proxima.RoundNumber, proxima.OverallPick,
            pendentes.Count(p => p.OverallPick < proxima.OverallPick), minhas.Count);
    }

    /// <summary>Lista do draft em andamento, se o time tem escolhas nele; senão, a do próximo draft (se planejado).</summary>
    private async Task<MinhaAreaAutoPickDto?> EscolhaAutomaticaAsync(Guid teamId, CancellationToken ct)
    {
        var draftId = await _db.Drafts.AsNoTracking()
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => (Guid?)d.DraftId)
            .FirstOrDefaultAsync(ct);

        var noDraft = draftId is Guid id
                      && await _db.DraftPicks.AnyAsync(p => p.DraftId == id && p.TeamId == teamId && p.PlayerId == null, ct);

        if (noDraft)
        {
            var config = await _db.DraftAutoPicks.AsNoTracking()
                .Where(c => c.DraftId == draftId && c.TeamId == teamId)
                .Select(c => new { c.Ativo, c.Modo })
                .FirstOrDefaultAsync(ct);
            var itens = await _db.DraftAutoPickItens.CountAsync(i => i.DraftId == draftId && i.TeamId == teamId, ct);
            return new MinhaAreaAutoPickDto(false, config is not null, config?.Ativo ?? false, config?.Modo ?? default, itens);
        }

        if (!await _db.DraftPlanejadoRodadas.AnyAsync(ct))
            return null;

        var previa = await _db.DraftAutoPickPrevias.AsNoTracking()
            .Where(p => p.TeamId == teamId)
            .Select(p => new { p.Ativo, p.Modo })
            .FirstOrDefaultAsync(ct);
        var itensPrevia = await _db.DraftAutoPickPreviaItens.CountAsync(i => i.TeamId == teamId, ct);
        return new MinhaAreaAutoPickDto(true, previa is not null, previa?.Ativo ?? false, previa?.Modo ?? default, itensPrevia);
    }

    /// <summary>Observados com o time atual e, se estiver num leilão aberto, o lance e quem lidera.</summary>
    private async Task<IReadOnlyList<MinhaAreaObservadoDto>> ObservadosAsync(Guid teamId, Func<Guid?, string?> nome, CancellationToken ct)
    {
        var observados = await _db.TeamObservacoes.AsNoTracking()
            .Where(o => o.TeamId == teamId)
            .OrderBy(o => o.CriadoEm)
            .Select(o => new
            {
                o.PlayerId, o.Player.Name, Posicao = o.Player.Position.Name, o.Player.Overall, o.Player.Age,
                TimeId = o.Player.TeamRosters.Select(r => (Guid?)r.TeamId).FirstOrDefault()
            })
            .ToListAsync(ct);

        var ids = observados.Select(o => o.PlayerId).ToList();
        var leiloes = (await _db.MarketItems.AsNoTracking()
                .Where(i => ids.Contains(i.PlayerId) && i.Status == MarketItemStatus.Active)
                .Select(i => new { i.PlayerId, Atual = i.CurrentLeaderAmount ?? i.BasePrice, i.CurrentLeaderTeamId, i.ExpiresAtUtc })
                .ToListAsync(ct))
            .GroupBy(i => i.PlayerId)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.ExpiresAtUtc).First());

        return observados
            .Select(o =>
            {
                leiloes.TryGetValue(o.PlayerId, out var l);
                return new MinhaAreaObservadoDto(o.PlayerId, o.Name, o.Posicao, o.Overall, o.Age, nome(o.TimeId),
                    l?.Atual, l is null ? null : nome(l.CurrentLeaderTeamId), l?.ExpiresAtUtc, l?.CurrentLeaderTeamId == teamId);
            })
            // Quem está em leilão agora aparece primeiro.
            .OrderByDescending(o => o.LeilaoLanceAtual is not null)
            .ThenByDescending(o => o.Overall)
            .ToList();
    }
}
