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
            .Select(t => new { t.TeamName, t.OwnerName, t.Budget, t.BudgetBlocked, Elenco = t.Roster.Count, t.MinRosterSizeOverride })
            .FirstAsync(ct);
        var minimoGeral = await _db.TransferConfigs.AsNoTracking().Select(c => (int?)c.MinRosterSize).FirstOrDefaultAsync(ct)
                          ?? TransferConfig.Default().MinRosterSize;

        var nomes = await _db.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);
        string? Nome(Guid? id) => id is Guid g && nomes.TryGetValue(g, out var n) ? n : null;
        var (disciplina, suspensos) = await DisciplinaAsync(teamId, ct);

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
            await ObservadosAsync(teamId, Nome, ct),
            time.MinRosterSizeOverride ?? minimoGeral,
            disciplina,
            await PendenciasAsync(teamId, suspensos, ct));
    }

    /// <summary>
    /// O que o treinador ainda precisa fazer: lista de protegidos do draft de expansão, pré-draft aberto
    /// e escalação ativa com vaga vazia, jogador que saiu do elenco ou suspenso.
    /// </summary>
    private async Task<IReadOnlyList<MinhaAreaPendenciaDto>> PendenciasAsync(
        Guid teamId, IReadOnlyDictionary<int, string> suspensos, CancellationToken ct)
    {
        var pendencias = new List<MinhaAreaPendenciaDto>();

        // Draft de expansão com as listas de protegidos abertas: os times que já existiam precisam mandar a sua.
        var expansao = await _db.Drafts.AsNoTracking()
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new { d.DraftId, d.Tipo, d.ProtecaoEncerradaEm, d.ProtegidosPorTime })
            .FirstOrDefaultAsync(ct);
        if (expansao is { Tipo: Core.Enums.DraftTipo.Expansao, ProtecaoEncerradaEm: null }
            && !await _db.DraftPicks.AnyAsync(p => p.DraftId == expansao.DraftId && p.TeamId == teamId, ct)
            && !await _db.DraftProtecoes.AnyAsync(p => p.DraftId == expansao.DraftId && p.TeamId == teamId, ct))
            pendencias.Add(new MinhaAreaPendenciaDto(
                $"Mande sua lista de protegidos do draft de expansão ({expansao.ProtegidosPorTime} jogadores). Quem não mandar fica com os maiores overalls.",
                "/draft/protecao"));

        // Pré-draft aberto e o time ainda não montou a lista.
        var preDraft = await _db.DraftWishlistEdicoes.AsNoTracking()
            .Where(e => e.EncerradoEm == null)
            .OrderByDescending(e => e.Numero)
            .Select(e => (int?)e.Numero)
            .FirstOrDefaultAsync(ct);
        if (preDraft is int edicao && !await _db.DraftWishlistEntries.AnyAsync(w => w.Versao == edicao && w.TeamId == teamId, ct))
            pendencias.Add(new MinhaAreaPendenciaDto("O pré-draft está aberto e você ainda não montou sua lista.", "/draft/pre-draft"));

        // Escalação ativa: vaga de titular vazia, jogador que não é mais do elenco e suspenso no próximo jogo.
        var linkEscalacao = $"/teams/{teamId}/lineups";
        var escalacao = await _db.TeamLineups.AsNoTracking()
            .Where(l => l.TeamId == teamId && l.IsActive)
            .Select(l => new { l.Name, Slots = l.Slots.Select(s => new { s.IsBench, s.PlayerId, Nome = s.Player != null ? s.Player.Name : null }).ToList() })
            .FirstOrDefaultAsync(ct);
        if (escalacao is null)
        {
            pendencias.Add(new MinhaAreaPendenciaDto("Você não tem escalação ativa.", linkEscalacao));
        }
        else
        {
            var elenco = (await _db.TeamRosters.AsNoTracking().Where(r => r.TeamId == teamId).Select(r => r.PlayerId).ToListAsync(ct)).ToHashSet();
            var titulares = escalacao.Slots.Where(s => !s.IsBench).ToList();

            var vazias = titulares.Count(s => s.PlayerId is null);
            if (vazias > 0)
                pendencias.Add(new MinhaAreaPendenciaDto(
                    $"A escalação \"{escalacao.Name}\" tem {vazias} vaga{(vazias == 1 ? "" : "s")} de titular sem jogador.", linkEscalacao));

            var sairam = escalacao.Slots.Where(s => s.PlayerId is int id && !elenco.Contains(id)).Select(s => s.Nome ?? "?").ToList();
            if (sairam.Count > 0)
                pendencias.Add(new MinhaAreaPendenciaDto(
                    $"A escalação \"{escalacao.Name}\" tem quem não é mais do elenco: {string.Join(", ", sairam)}.", linkEscalacao));

            foreach (var s in titulares.Where(s => s.PlayerId is int id && suspensos.ContainsKey(id)))
                pendencias.Add(new MinhaAreaPendenciaDto(
                    $"{s.Nome} está escalado como titular, mas está suspenso ({suspensos[s.PlayerId!.Value]}).", linkEscalacao));
        }

        return pendencias;
    }

    /// <summary>
    /// Suspensos para o próximo jogo e pendurados do time nas Ligas e Copas em andamento, e os suspensos
    /// por jogador (para conferir a escalação).
    /// </summary>
    private async Task<(IReadOnlyList<MinhaAreaDisciplinaDto> Lista, IReadOnlyDictionary<int, string> Suspensos)> DisciplinaAsync(
        Guid teamId, CancellationToken ct)
    {
        var ligas = await _db.Ligas.AsNoTracking()
            .Where(l => (l.Tipo == Core.Enums.TipoCompetition.Liga || l.Tipo == Core.Enums.TipoCompetition.Copa)
                        && l.Status != Core.Enums.LigaStatus.Encerrada
                        && l.Rodadas.Any(r => r.Partidas.Any(p => p.TimeCasaId == teamId || p.TimeForaId == teamId)))
            .Select(l => new { l.LigaId, l.Nome })
            .ToListAsync(ct);

        var lista = new List<MinhaAreaDisciplinaDto>();
        var suspensos = new Dictionary<int, string>();
        foreach (var liga in ligas)
        {
            if (await DisciplinaDaCompeticao.CalcularAsync(_db, liga.LigaId, ct) is not { } d) continue;

            foreach (var s in d.Suspensoes.Where(s => s.TimeId == teamId && !s.Cumprida))
                suspensos.TryAdd(s.JogadorId, $"{liga.Nome}, {(s.JogoCumprido ?? "próximo jogo")}");

            lista.AddRange(d.Suspensoes
                .Where(s => s.TimeId == teamId && !s.Cumprida)
                .Select(s => new MinhaAreaDisciplinaDto(liga.Nome, s.JogadorNome, true,
                    $"{s.Motivo} ({s.JogoDoCartao}) · fora {(s.JogoCumprido is null ? "do próximo jogo" : $"de {s.JogoCumprido}")}")));
            lista.AddRange(d.Pendurados
                .Where(p => p.TimeId == teamId)
                .Select(p => new MinhaAreaDisciplinaDto(liga.Nome, p.JogadorNome, false, $"{p.Amarelos} amarelos · o próximo suspende")));
        }
        return (lista, suspensos);
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
