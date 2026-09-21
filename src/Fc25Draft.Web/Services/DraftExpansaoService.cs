using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Services;

/// <summary>
/// Draft de expansão: os times existentes protegem parte do elenco e os times novos escolhem
/// entre os demais. Quem perde jogador recebe compensação (valor de mercado × fator).
/// As escolhas em si passam pelo <see cref="DraftStateService"/>, como num draft normal.
/// </summary>
public class DraftExpansaoService
{
    private const string OrigemCompensacao = "DRAFT_EXPANSAO";

    private readonly DraftDbContext _db;
    private readonly DraftService _draftService;
    private readonly IPricingService _pricing;

    public DraftExpansaoService(DraftDbContext db, DraftService draftService, IPricingService pricing)
    {
        _db = db;
        _draftService = draftService;
        _pricing = pricing;
    }

    // ── Criação ──────────────────────────────────────────────────────────────

    public async Task<Draft> CriarAsync(DraftExpansaoCriarRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
            throw new InvalidOperationException("Informe o nome do draft.");
        if (request.Rodadas < 1)
            throw new InvalidOperationException("O draft precisa de pelo menos 1 rodada.");
        if (request.ProtegidosPorTime < 0)
            throw new InvalidOperationException("A quantidade de protegidos não pode ser negativa.");
        if (request.MaxPerdasPorTime < 1)
            throw new InvalidOperationException("Cada time precisa poder perder pelo menos 1 jogador.");
        if (request.FatorCompensacao < 0)
            throw new InvalidOperationException("O fator de compensação não pode ser negativo.");

        var novos = request.TimesNovos.Distinct().ToList();
        if (novos.Count == 0)
            throw new InvalidOperationException("Selecione os times novos.");

        await _draftService.EnsureNoDraftInProgressAsync(ct);

        // Capacidade: cada time existente cede no máximo o limite de perdas e nunca um protegido.
        var elencos = await _db.Teams
            .AsNoTracking()
            .Where(t => !novos.Contains(t.TeamId))
            .Select(t => new { t.TeamName, Elenco = t.Roster.Count })
            .Where(t => t.Elenco > 0)
            .ToListAsync(ct);

        var totalEscolhas = novos.Count * request.Rodadas;
        var capacidade = elencos.Sum(t => Math.Min(request.MaxPerdasPorTime, Math.Max(0, t.Elenco - request.ProtegidosPorTime)));
        if (totalEscolhas > capacidade)
            throw new InvalidOperationException(
                $"São {totalEscolhas} escolhas, mas os times existentes só podem ceder {capacidade} jogadores " +
                $"({elencos.Count} times, até {request.MaxPerdasPorTime} cada, protegendo {request.ProtegidosPorTime}).");

        return await _draftService.CreateDraftAsync(
            request.Nome.Trim(),
            novos,
            request.Rodadas,
            request.Serpentina,
            roundRules: null,
            ct,
            shuffleOrder: request.SortearOrdem,
            configure: d =>
            {
                d.Tipo = DraftTipo.Expansao;
                d.ProtegidosPorTime = request.ProtegidosPorTime;
                d.MaxPerdasPorTime = request.MaxPerdasPorTime;
                d.FatorCompensacao = request.FatorCompensacao;
            });
    }

    /// <summary>Draft de jogadores livres só com os times novos da última expansão.</summary>
    public async Task<Draft> CriarDraftComplementarAsync(DraftComplementarRequest request, CancellationToken ct)
    {
        var expansao = await GetUltimaExpansaoAsync(ct)
            ?? throw new InvalidOperationException("Nenhum draft de expansão encontrado.");

        if (request.Rodadas < 1)
            throw new InvalidOperationException("O draft precisa de pelo menos 1 rodada.");
        if (request.OverallMin is int min && request.OverallMax is int max && min > max)
            throw new InvalidOperationException("O overall mínimo não pode ser maior que o máximo.");

        // Ordem da 1ª rodada da expansão (a menos que peça novo sorteio).
        var ordem = await _db.DraftPicks
            .AsNoTracking()
            .Where(p => p.DraftId == expansao.DraftId && p.RoundNumber == 1)
            .OrderBy(p => p.PickInRound)
            .Select(p => p.TeamId)
            .ToListAsync(ct);

        var regras = Enumerable.Range(1, request.Rodadas)
            .ToDictionary(r => r, _ => (request.OverallMin, request.OverallMax));

        return await _draftService.CreateDraftAsync(
            string.IsNullOrWhiteSpace(request.Nome) ? $"{expansao.Name} — Complementar" : request.Nome.Trim(),
            ordem,
            request.Rodadas,
            request.Serpentina,
            regras,
            ct,
            shuffleOrder: request.SortearOrdem);
    }

    // ── Proteção ─────────────────────────────────────────────────────────────

    public async Task<DraftProtecaoTimeDto?> GetProtecaoDoTimeAsync(Guid teamId, CancellationToken ct)
    {
        var draft = await GetExpansaoAtivaAsync(ct);
        if (draft is null) return null;

        var novos = await GetTimesNovosAsync(draft.DraftId, ct);
        if (novos.Contains(teamId)) return null;

        var team = await _db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.TeamId == teamId, ct);
        if (team is null) return null;

        var protegidos = await _db.DraftProtecoes
            .AsNoTracking()
            .Where(p => p.DraftId == draft.DraftId && p.TeamId == teamId)
            .ToDictionaryAsync(p => p.PlayerId, p => p.EscolhidoPeloTime, ct);

        var elenco = await _db.TeamRosters
            .AsNoTracking()
            .Where(r => r.TeamId == teamId)
            .Select(r => r.Player)
            .OrderByDescending(p => p.Overall)
            .ThenBy(p => p.Name)
            .Select(p => new { p.PlayerId, p.Name, Posicao = p.Position.Name, p.Overall, p.Age })
            .ToListAsync(ct);

        return new DraftProtecaoTimeDto(
            draft.DraftId,
            draft.Name,
            team.TeamId,
            team.TeamName,
            draft.ProtegidosPorTime ?? 0,
            draft.MaxPerdasPorTime ?? 0,
            await ProtecaoAbertaAsync(draft, ct),
            elenco.Select(p => new DraftProtecaoJogadorDto(
                p.PlayerId, p.Name, p.Posicao, p.Overall, p.Age,
                protegidos.ContainsKey(p.PlayerId),
                protegidos.TryGetValue(p.PlayerId, out var peloTime) && !peloTime)).ToList());
    }

    public async Task SalvarProtecaoAsync(Guid teamId, IReadOnlyCollection<int> playerIds, CancellationToken ct)
    {
        var draft = await GetExpansaoAtivaAsync(ct)
            ?? throw new InvalidOperationException("Não há draft de expansão em andamento.");

        if (!await ProtecaoAbertaAsync(draft, ct))
            throw new InvalidOperationException("As listas de protegidos já foram encerradas.");

        var novos = await GetTimesNovosAsync(draft.DraftId, ct);
        if (novos.Contains(teamId))
            throw new InvalidOperationException("Times novos não protegem jogadores.");

        var ids = playerIds.Distinct().ToList();
        var elenco = await _db.TeamRosters
            .Where(r => r.TeamId == teamId)
            .Select(r => r.PlayerId)
            .ToListAsync(ct);

        if (ids.Any(id => !elenco.Contains(id)))
            throw new InvalidOperationException("Só é possível proteger jogadores do próprio elenco.");

        var exigidos = Math.Min(draft.ProtegidosPorTime ?? 0, elenco.Count);
        if (ids.Count != exigidos)
            throw new InvalidOperationException($"Selecione exatamente {exigidos} jogadores para proteger.");

        var agora = DateTime.UtcNow;
        var anteriores = await _db.DraftProtecoes
            .Where(p => p.DraftId == draft.DraftId && p.TeamId == teamId)
            .ToListAsync(ct);
        _db.DraftProtecoes.RemoveRange(anteriores);
        _db.DraftProtecoes.AddRange(ids.Select(id => new DraftProtecao
        {
            DraftId = draft.DraftId,
            TeamId = teamId,
            PlayerId = id,
            EscolhidoPeloTime = true,
            CriadoEm = agora
        }));

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Fecha as listas e libera as escolhas. Quem não enviou (ou perdeu protegido por transferência)
    /// tem a lista completada com os maiores overalls do elenco.
    /// </summary>
    public async Task EncerrarProtecaoAsync(CancellationToken ct)
    {
        var draft = await GetExpansaoAtivaAsync(ct, rastrear: true)
            ?? throw new InvalidOperationException("Não há draft de expansão em andamento.");

        if (draft.ProtecaoEncerradaEm is not null)
            throw new InvalidOperationException("As listas de protegidos já foram encerradas.");

        var novos = await GetTimesNovosAsync(draft.DraftId, ct);
        var protegidosPorTime = draft.ProtegidosPorTime ?? 0;
        var agora = DateTime.UtcNow;

        var elencos = await _db.TeamRosters
            .Where(r => !novos.Contains(r.TeamId))
            .Select(r => new { r.TeamId, r.PlayerId, r.Player.Overall, r.Player.Name })
            .ToListAsync(ct);

        var protecoes = await _db.DraftProtecoes
            .Where(p => p.DraftId == draft.DraftId)
            .ToListAsync(ct);

        // Protegido que já não está no elenco do time não vale mais.
        var noElenco = elencos.Select(e => (e.TeamId, e.PlayerId)).ToHashSet();
        _db.DraftProtecoes.RemoveRange(protecoes.Where(p => !noElenco.Contains((p.TeamId, p.PlayerId))));

        foreach (var time in elencos.GroupBy(e => e.TeamId))
        {
            var jaProtegidos = protecoes
                .Where(p => p.TeamId == time.Key && noElenco.Contains((p.TeamId, p.PlayerId)))
                .Select(p => p.PlayerId)
                .ToHashSet();

            var faltam = Math.Min(protegidosPorTime, time.Count()) - jaProtegidos.Count;
            if (faltam <= 0) continue;

            var completar = time
                .Where(j => !jaProtegidos.Contains(j.PlayerId))
                .OrderByDescending(j => j.Overall)
                .ThenBy(j => j.Name)
                .Take(faltam);

            _db.DraftProtecoes.AddRange(completar.Select(j => new DraftProtecao
            {
                DraftId = draft.DraftId,
                TeamId = time.Key,
                PlayerId = j.PlayerId,
                EscolhidoPeloTime = false,
                CriadoEm = agora
            }));
        }

        draft.ProtecaoEncerradaEm = agora;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReabrirProtecaoAsync(CancellationToken ct)
    {
        var draft = await GetExpansaoAtivaAsync(ct, rastrear: true)
            ?? throw new InvalidOperationException("Não há draft de expansão em andamento.");

        if (await _db.DraftPicks.AnyAsync(p => p.DraftId == draft.DraftId && p.PlayerId != null, ct))
            throw new InvalidOperationException("Não dá para reabrir: as escolhas já começaram.");

        // Remove o que o sistema completou sozinho; as listas enviadas pelos times continuam.
        var automaticas = await _db.DraftProtecoes
            .Where(p => p.DraftId == draft.DraftId && !p.EscolhidoPeloTime)
            .ToListAsync(ct);
        _db.DraftProtecoes.RemoveRange(automaticas);

        draft.ProtecaoEncerradaEm = null;
        await _db.SaveChangesAsync(ct);
    }

    // ── Resumo (admin) ───────────────────────────────────────────────────────

    public async Task<DraftExpansaoResumoDto?> GetResumoAsync(CancellationToken ct)
    {
        var draft = await GetUltimaExpansaoAsync(ct);
        if (draft is null) return null;

        var picks = await _db.DraftPicks
            .AsNoTracking()
            .Where(p => p.DraftId == draft.DraftId)
            .OrderBy(p => p.OverallPick)
            .Select(p => new
            {
                p.OverallPick,
                p.RoundNumber,
                p.TeamId,
                TimeNovo = p.Team.TeamName,
                Jogador = p.Player != null ? p.Player.Name : null,
                Overall = p.Player != null ? (int?)p.Player.Overall : null,
                p.FromTeamId,
                TimeOrigem = p.FromTeam != null ? p.FromTeam.TeamName : null,
                p.Compensacao
            })
            .ToListAsync(ct);

        var novos = picks.Select(p => p.TeamId).Distinct().ToList();

        var protecoes = await _db.DraftProtecoes
            .AsNoTracking()
            .Where(p => p.DraftId == draft.DraftId)
            .GroupBy(p => p.TeamId)
            .Select(g => new { TeamId = g.Key, Total = g.Count(), Automatica = g.Any(p => !p.EscolhidoPeloTime) })
            .ToDictionaryAsync(x => x.TeamId, ct);

        var times = await _db.Teams
            .AsNoTracking()
            .OrderBy(t => t.TeamName)
            .Select(t => new { t.TeamId, t.TeamName, t.OwnerName, Elenco = t.Roster.Count, t.MinRosterSizeOverride, t.Budget })
            .ToListAsync(ct);

        var existentes = times
            .Where(t => !novos.Contains(t.TeamId))
            .Select(t =>
            {
                var perdas = picks.Where(p => p.FromTeamId == t.TeamId).ToList();
                protecoes.TryGetValue(t.TeamId, out var protecao);
                return new
                {
                    Dto = new DraftExpansaoTimeExistenteDto(
                        t.TeamId, t.TeamName, t.OwnerName, t.Elenco,
                        protecao?.Total ?? 0, protecao?.Automatica ?? false,
                        perdas.Count, perdas.Sum(p => p.Compensacao ?? 0m), t.MinRosterSizeOverride),
                    t.Budget,
                    Participa = t.Elenco > 0 || perdas.Count > 0 || protecao is not null
                };
            })
            .Where(x => x.Participa)
            .ToList();

        var timesNovos = times
            .Where(t => novos.Contains(t.TeamId))
            .Select(t => new DraftExpansaoTimeNovoDto(
                t.TeamId, t.TeamName, t.OwnerName, t.Elenco,
                picks.Count(p => p.TeamId == t.TeamId && p.Jogador != null),
                t.Budget))
            .ToList();

        return new DraftExpansaoResumoDto(
            draft.DraftId,
            draft.Name,
            draft.ProtegidosPorTime ?? 0,
            draft.MaxPerdasPorTime ?? 0,
            draft.FatorCompensacao ?? 0m,
            draft.ProtecaoEncerradaEm is not null,
            picks.Count,
            picks.Count(p => p.Jogador != null),
            existentes.Select(x => x.Dto).ToList(),
            timesNovos,
            picks.Select(p => new DraftExpansaoEscolhaDto(
                p.OverallPick, p.RoundNumber, p.TimeNovo, p.Jogador, p.Overall, p.TimeOrigem, p.Compensacao)).ToList(),
            existentes.Count > 0 ? decimal.Round(existentes.Average(x => x.Budget), 2) : 0m);
    }

    /// <summary>
    /// Completa o caixa de cada time novo da última expansão até <paramref name="valor"/>
    /// (credita só a diferença, então repetir não duplica). Retorna quanto foi creditado no total.
    /// </summary>
    public async Task<decimal> IgualarCaixaTimesNovosAsync(decimal valor, CancellationToken ct)
    {
        if (valor <= 0)
            throw new InvalidOperationException("Informe um valor de caixa maior que zero.");

        var draft = await GetUltimaExpansaoAsync(ct)
            ?? throw new InvalidOperationException("Nenhum draft de expansão encontrado.");

        var novos = await GetTimesNovosAsync(draft.DraftId, ct);
        var times = await _db.Teams.Where(t => novos.Contains(t.TeamId)).ToListAsync(ct);
        var agora = DateTime.UtcNow;
        var total = 0m;

        foreach (var time in times.Where(t => t.Budget < valor))
        {
            var diferenca = decimal.Round(valor - time.Budget, 2, MidpointRounding.AwayFromZero);
            time.Budget += diferenca;
            total += diferenca;
            _db.BudgetLedgers.Add(new BudgetLedger
            {
                BudgetLedgerId = Guid.NewGuid(),
                TeamId = time.TeamId,
                DataUtc = agora,
                Tipo = "CREDIT",
                Origem = "EXPANSAO_CAIXA_INICIAL",
                Valor = diferenca,
                Descricao = $"Caixa inicial de time novo ({draft.Name})"
            });
        }

        await _db.SaveChangesAsync(ct);
        return total;
    }

    // ── Regras usadas pelo DraftStateService ─────────────────────────────────

    /// <summary>Jogadores que podem ser escolhidos agora: de times existentes abaixo do limite de perdas e não protegidos.</summary>
    public async Task<IQueryable<Player>> FiltrarDisponiveisAsync(IQueryable<Player> jogadores, Draft draft, CancellationToken ct)
    {
        if (draft.ProtecaoEncerradaEm is null)
            return jogadores.Where(_ => false);

        var novos = await GetTimesNovosAsync(draft.DraftId, ct);
        var bloqueados = await GetTimesNoLimiteDePerdasAsync(draft, ct);
        var draftId = draft.DraftId;

        return jogadores.Where(p =>
            p.TeamRosters.Any(r => !novos.Contains(r.TeamId) && !bloqueados.Contains(r.TeamId))
            && !_db.DraftProtecoes.Any(x => x.DraftId == draftId && x.PlayerId == p.PlayerId));
    }

    /// <summary>
    /// Transfere o jogador escolhido do time existente para o time novo, registra no histórico
    /// e credita a compensação. Deve rodar dentro da transação da escolha; não salva.
    /// </summary>
    public async Task<Team> ExecutarEscolhaAsync(Draft draft, DraftPick pick, Player player, CancellationToken ct)
    {
        if (draft.ProtecaoEncerradaEm is null)
            throw new InvalidOperationException("⏳ As listas de protegidos ainda estão abertas. Aguarde o admin encerrá-las.");

        var roster = await _db.TeamRosters
            .Include(r => r.Team)
            .FirstOrDefaultAsync(r => r.PlayerId == player.PlayerId, ct)
            ?? throw new InvalidOperationException("❌ Este jogador não está em nenhum elenco; no draft de expansão só valem jogadores dos times existentes.");

        var novos = await GetTimesNovosAsync(draft.DraftId, ct);
        if (novos.Contains(roster.TeamId))
            throw new InvalidOperationException("❌ Este jogador já pertence a um time novo.");

        if (await _db.DraftProtecoes.AnyAsync(p => p.DraftId == draft.DraftId && p.PlayerId == player.PlayerId, ct))
            throw new InvalidOperationException($"🛡️ {player.Name} está protegido pelo {roster.Team.TeamName}.");

        var bloqueados = await GetTimesNoLimiteDePerdasAsync(draft, ct);
        if (bloqueados.Contains(roster.TeamId))
            throw new InvalidOperationException($"❌ O {roster.Team.TeamName} já perdeu {draft.MaxPerdasPorTime} jogadores.");

        var origem = roster.Team;
        var valorMercado = (await _pricing.CalculateForPlayerAsync(player.PlayerId, ct)).BasePrice;
        var compensacao = decimal.Round(valorMercado * (draft.FatorCompensacao ?? 0m), 2, MidpointRounding.AwayFromZero);
        var agora = DateTime.UtcNow;

        _db.TeamRosters.Remove(roster);
        _db.TeamRosters.Add(new TeamRoster { TeamId = pick.TeamId, PlayerId = player.PlayerId });
        player.CurrentTeamId = pick.TeamId;

        pick.FromTeamId = origem.TeamId;
        pick.Compensacao = compensacao;

        _db.TransferHistories.Add(new TransferHistory
        {
            TransferId = Guid.NewGuid(),
            Type = TransferType.ExpansionDraft,
            PlayerId = player.PlayerId,
            FromTeamId = origem.TeamId,
            ToTeamId = pick.TeamId,
            Amount = compensacao,
            Notes = $"{draft.Name} — rodada {pick.RoundNumber}, escolha {pick.PickInRound}",
            PerformedBy = "draft-expansao",
            PerformedAtUtc = agora,
            OldOverall = player.Overall,
            NewOverall = player.Overall
        });

        if (compensacao > 0)
        {
            origem.Budget += compensacao;
            _db.BudgetLedgers.Add(new BudgetLedger
            {
                BudgetLedgerId = Guid.NewGuid(),
                TeamId = origem.TeamId,
                DataUtc = agora,
                Tipo = "CREDIT",
                Origem = OrigemCompensacao,
                Valor = compensacao,
                Descricao = $"Compensação por {player.Name} ({draft.Name})"
            });
        }

        return origem;
    }

    // ── Apoio ────────────────────────────────────────────────────────────────

    /// <summary>Time dono do token (titular ou auxiliar), ou nulo.</summary>
    public async Task<(Guid TeamId, string TeamName)?> ResolverTimeAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var normalizado = token.Trim();
        var time = await _db.Teams
            .AsNoTracking()
            .Where(t => t.Token == normalizado || t.AuxToken == normalizado)
            .Select(t => new { t.TeamId, t.TeamName })
            .FirstOrDefaultAsync(ct);

        return time is null ? null : (time.TeamId, time.TeamName);
    }

    public async Task<List<(Guid TeamId, string TeamName)>> ListarTimesAsync(CancellationToken ct)
    {
        var times = await _db.Teams
            .AsNoTracking()
            .OrderBy(t => t.TeamName)
            .Select(t => new { t.TeamId, t.TeamName })
            .ToListAsync(ct);

        return times.Select(t => (t.TeamId, t.TeamName)).ToList();
    }

    /// <summary>O draft ativo (o mais recente), se for de expansão.</summary>
    private async Task<Draft?> GetExpansaoAtivaAsync(CancellationToken ct, bool rastrear = false)
    {
        var query = rastrear ? _db.Drafts : _db.Drafts.AsNoTracking();
        var draft = await query.OrderByDescending(d => d.CreatedAtUtc).FirstOrDefaultAsync(ct);
        return draft?.Tipo == DraftTipo.Expansao ? draft : null;
    }

    private Task<Draft?> GetUltimaExpansaoAsync(CancellationToken ct) =>
        _db.Drafts
            .AsNoTracking()
            .Where(d => d.Tipo == DraftTipo.Expansao)
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    private async Task<bool> ProtecaoAbertaAsync(Draft draft, CancellationToken ct) =>
        draft.ProtecaoEncerradaEm is null
        && await _db.DraftPicks.AnyAsync(p => p.DraftId == draft.DraftId && p.PlayerId == null, ct);

    private Task<List<Guid>> GetTimesNovosAsync(Guid draftId, CancellationToken ct) =>
        _db.DraftPicks
            .Where(p => p.DraftId == draftId)
            .Select(p => p.TeamId)
            .Distinct()
            .ToListAsync(ct);

    private Task<List<Guid>> GetTimesNoLimiteDePerdasAsync(Draft draft, CancellationToken ct)
    {
        var limite = draft.MaxPerdasPorTime ?? int.MaxValue;
        return _db.DraftPicks
            .Where(p => p.DraftId == draft.DraftId && p.FromTeamId != null)
            .GroupBy(p => p.FromTeamId!.Value)
            .Where(g => g.Count() >= limite)
            .Select(g => g.Key)
            .ToListAsync(ct);
    }
}
