using System;
using System.Collections.Generic;
using System.Linq;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Infra.Data;
using Fc25Draft.Web.Hubs;
using Fc25Draft.Web.Security;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fc25Draft.Web.Services;

public class DraftStateService
{
    private readonly DraftDbContext _db;
    private readonly IHubContext<DraftHub> _hubContext;
    private readonly ILogger<DraftStateService> _logger;
    private readonly SecurityOptions _securityOptions;
    private readonly DraftExpansaoService _expansao;

    public DraftStateService(
        DraftDbContext db,
        IHubContext<DraftHub> hubContext,
        ILogger<DraftStateService> logger,
        IOptions<SecurityOptions> securityOptions,
        DraftExpansaoService expansao)
    {
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
        _securityOptions = securityOptions.Value;
        _expansao = expansao;
    }

    public async Task<DraftStateDto> GetStateAsync(CancellationToken ct = default)
    {
        var draft = await _db.Drafts
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (draft is null)
        {
            return DraftStateDto.Empty;
        }

        var picksQuery = _db.DraftPicks
            .AsNoTracking()
            .Where(p => p.DraftId == draft.DraftId);

        var totalPicks = await picksQuery.CountAsync(ct);
        var completedPicks = await picksQuery.Where(p => p.PlayerId != null).CountAsync(ct);

        var currentPick = await picksQuery
            .Where(p => p.PlayerId == null)
            .OrderBy(p => p.OverallPick)
            .Select(p => new
            {
                p.RoundNumber,
                p.PickInRound,
                p.OverallPick,
                TeamId = p.Team.TeamId,
                TeamName = p.Team.TeamName,
                TeamOwner = p.Team.OwnerName
            })
            .FirstOrDefaultAsync(ct);

        Guid? nextTeamId = null;
        string? nextTeamName = null;
        string? nextTeamOwner = null;

        if (currentPick is not null)
        {
            var nextPick = await picksQuery
                .Where(p => p.PlayerId == null && p.OverallPick > currentPick.OverallPick)
                .OrderBy(p => p.OverallPick)
                .Select(p => new
                {
                    TeamId = p.Team.TeamId,
                    TeamName = p.Team.TeamName,
                    TeamOwner = p.Team.OwnerName
                })
                .FirstOrDefaultAsync(ct);

            if (nextPick is not null)
            {
                nextTeamId = nextPick.TeamId;
                nextTeamName = nextPick.TeamName;
                nextTeamOwner = nextPick.TeamOwner;
            }
        }

        return new DraftStateDto(
            draft.DraftId,
            draft.Name,
            draft.TotalTeams,
            draft.TotalRounds,
            totalPicks,
            completedPicks,
            currentPick?.RoundNumber,
            currentPick?.PickInRound,
            currentPick?.OverallPick,
            currentPick?.TeamId,
            currentPick?.TeamName,
            currentPick?.TeamOwner,
            nextTeamId,
            nextTeamName,
            nextTeamOwner,
            currentPick is null && totalPicks > 0 && completedPicks == totalPicks,
            draft.Tipo == DraftTipo.Expansao,
            draft.Tipo == DraftTipo.Expansao && draft.ProtecaoEncerradaEm is null && currentPick is not null);
    }

    public async Task<IReadOnlyList<AvailablePlayerDto>> GetAvailablePlayersAsync(
        IReadOnlyCollection<short>? positionIds,
        string? searchTerm = null,
        int? overallMinFilter = null,
        int? overallMaxFilter = null,
        CancellationToken ct = default)
        => await BuscarDisponiveisAsync(positionIds, searchTerm, overallMinFilter, overallMaxFilter, limitarARodadaAtual: true, ct);

    /// <summary>
    /// Jogadores que ainda podem ser escolhidos. Com <paramref name="limitarARodadaAtual"/> aplica o
    /// overall permitido na rodada atual; sem ele (listas da escolha automática) vale o draft inteiro.
    /// </summary>
    public async Task<IReadOnlyList<AvailablePlayerDto>> BuscarDisponiveisAsync(
        IReadOnlyCollection<short>? positionIds,
        string? searchTerm,
        int? overallMinFilter,
        int? overallMaxFilter,
        bool limitarARodadaAtual,
        CancellationToken ct = default)
    {
        var draft = await _db.Drafts
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (draft is null)
        {
            return Array.Empty<AvailablePlayerDto>();
        }

        var currentRoundNumber = await _db.DraftPicks
            .Where(p => p.DraftId == draft.DraftId && p.PlayerId == null)
            .OrderBy(p => p.OverallPick)
            .Select(p => (int?)p.RoundNumber)
            .FirstOrDefaultAsync(ct);

        if (!currentRoundNumber.HasValue)
        {
            return Array.Empty<AvailablePlayerDto>();
        }

        var query = await ConsultaDisponiveisAsync(draft, limitarARodadaAtual ? currentRoundNumber : null, ct);
        return await FiltrarEListarAsync(query, positionIds, searchTerm, overallMinFilter, overallMaxFilter, ct);
    }

    /// <summary>
    /// Jogadores livres (sem time), para as listas do próximo draft: ele ainda não existe, então vale
    /// o pool de um draft normal, não o do draft em andamento (que pode ser de expansão).
    /// </summary>
    public async Task<IReadOnlyList<AvailablePlayerDto>> BuscarLivresAsync(
        IReadOnlyCollection<short>? positionIds,
        string? searchTerm,
        int? overallMinFilter,
        int? overallMaxFilter,
        CancellationToken ct = default)
        => await FiltrarEListarAsync(Livres(), positionIds, searchTerm, overallMinFilter, overallMaxFilter, ct);

    /// <summary>Dos jogadores informados, os que continuam sem time.</summary>
    public async Task<HashSet<int>> FiltrarLivresAsync(IReadOnlyCollection<int> playerIds, CancellationToken ct = default)
    {
        if (playerIds.Count == 0)
        {
            return new HashSet<int>();
        }

        var ids = await Livres()
            .Where(p => playerIds.Contains(p.PlayerId))
            .Select(p => p.PlayerId)
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    private IQueryable<Player> Livres() => _db.Players.AsNoTracking().Where(p => !p.TeamRosters.Any());

    private static async Task<IReadOnlyList<AvailablePlayerDto>> FiltrarEListarAsync(
        IQueryable<Player> query,
        IReadOnlyCollection<short>? positionIds,
        string? searchTerm,
        int? overallMinFilter,
        int? overallMaxFilter,
        CancellationToken ct)
    {
        if (positionIds is { Count: > 0 })
        {
            // List, não array: com C# 14 o array.Contains vira o overload de Span, que o EF 8 não traduz.
            var filterPositions = positionIds
                .Distinct()
                .ToList();

            if (filterPositions.Count > 0)
            {
                query = query.Where(p => filterPositions.Contains(p.PositionId));
            }
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(
                EF.Functions.Unaccent(p.Name),
                EF.Functions.Unaccent(pattern)));
        }

        if (overallMinFilter.HasValue)
        {
            query = query.Where(p => p.Overall >= overallMinFilter.Value);
        }

        if (overallMaxFilter.HasValue)
        {
            query = query.Where(p => p.Overall <= overallMaxFilter.Value);
        }

        var players = await query
            .OrderByDescending(p => p.Overall)
            .ThenBy(p => p.Name)
            .Select(p => new AvailablePlayerDto(
                p.PlayerId,
                p.Name,
                p.PositionId,
                p.Position.Name,
                p.Overall,
                p.Age,
                p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault()))
            .ToListAsync(ct);

        return players;
    }

    /// <summary>Jogadores escolhíveis no draft; com <paramref name="roundNumber"/>, só os do overall permitido na rodada.</summary>
    private async Task<IQueryable<Player>> ConsultaDisponiveisAsync(Draft draft, int? roundNumber, CancellationToken ct)
    {
        var query = draft.Tipo == DraftTipo.Expansao
            ? await _expansao.FiltrarDisponiveisAsync(_db.Players.AsNoTracking(), draft, ct)
            : Livres();

        if (roundNumber is null)
        {
            return query;
        }

        var round = await _db.DraftRounds
            .AsNoTracking()
            .Where(r => r.DraftId == draft.DraftId && r.RoundNumber == roundNumber.Value)
            .Select(r => new { r.OverallMin, r.OverallMax })
            .FirstOrDefaultAsync(ct);

        if (round?.OverallMin is int overallMin)
        {
            query = query.Where(p => p.Overall >= overallMin);
        }

        if (round?.OverallMax is int overallMax)
        {
            query = query.Where(p => p.Overall <= overallMax);
        }

        return query;
    }

    /// <summary>Dos jogadores informados, os que ainda podem ser escolhidos no draft atual (sem olhar a rodada).</summary>
    public async Task<HashSet<int>> FiltrarDisponiveisAsync(IReadOnlyCollection<int> playerIds, CancellationToken ct = default)
    {
        if (playerIds.Count == 0)
        {
            return new HashSet<int>();
        }

        var draft = await _db.Drafts
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (draft is null)
        {
            return new HashSet<int>();
        }

        var ids = await (await ConsultaDisponiveisAsync(draft, null, ct))
            .Where(p => playerIds.Contains(p.PlayerId))
            .Select(p => p.PlayerId)
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    // Escolha manual + a sequência automática que ela dispara rodam uma de cada vez,
    // para duas sequências não disputarem a mesma escolha.
    private static readonly SemaphoreSlim PickLock = new(1, 1);

    /// <summary>Limite de segurança da sequência automática (bem acima de qualquer draft real).</summary>
    private const int MaxEscolhasAutomaticasSeguidas = 1000;

    private sealed record EscolhaRegistrada(DraftPick Pick, Player Player, string? FromTeam, bool Automatica);

    public async Task<DraftPickResultDto> MakePickAsync(int playerId, string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("O token do time é obrigatório.", nameof(token));
        }

        await PickLock.WaitAsync(ct);
        try
        {
            var escolhas = new List<EscolhaRegistrada> { await RegistrarEscolhaManualAsync(playerId, token.Trim(), ct) };
            escolhas.AddRange(await ExecutarAutomaticasAsync(ct));
            return await FinalizarSequenciaAsync(escolhas, ct);
        }
        finally
        {
            PickLock.Release();
        }
    }

    /// <summary>
    /// Faz as escolhas automáticas a partir da vez atual (ex.: logo depois de um time salvar a lista
    /// quando já era a vez dele). Nulo se nenhuma escolha foi feita.
    /// </summary>
    public async Task<DraftPickResultDto?> ProcessarAutomaticasAsync(CancellationToken ct = default)
    {
        await PickLock.WaitAsync(ct);
        try
        {
            var escolhas = await ExecutarAutomaticasAsync(ct);
            return escolhas.Count == 0 ? null : await FinalizarSequenciaAsync(escolhas, ct);
        }
        finally
        {
            PickLock.Release();
        }
    }

    private async Task<EscolhaRegistrada> RegistrarEscolhaManualAsync(int playerId, string normalizedToken, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            var draft = await _db.Drafts
                .OrderByDescending(d => d.CreatedAtUtc)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Nenhum draft ativo foi encontrado.");

            var currentPick = await _db.DraftPicks
                .Include(p => p.Team)
                .Where(p => p.DraftId == draft.DraftId && p.PlayerId == null)
                .OrderBy(p => p.OverallPick)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Todas as escolhas já foram realizadas.");

            var roundLimits = await _db.DraftRounds
                .AsNoTracking()
                .Where(r => r.DraftId == draft.DraftId && r.RoundNumber == currentPick.RoundNumber)
                .Select(r => new { r.OverallMin, r.OverallMax })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Não foi possível localizar as regras da rodada atual.");

            var isTeamToken = string.Equals(normalizedToken, currentPick.Team.Token, StringComparison.OrdinalIgnoreCase)
                || (currentPick.Team.AuxToken is not null && string.Equals(normalizedToken, currentPick.Team.AuxToken, StringComparison.OrdinalIgnoreCase));
            if (!isTeamToken)
            {
                var isAdmin = await _db.AdminTokens
                    .AsNoTracking()
                    .AnyAsync(t => t.Token == normalizedToken && t.IsActive, ct)
                    || await _db.Teams
                        .AsNoTracking()
                        .AnyAsync(t => (t.Token == normalizedToken || t.AuxToken == normalizedToken) && t.IsAdmin, ct);

                if (!isAdmin)
                    throw new InvalidOperationException("⚠️ Token inválido para este time.");
            }

            var player = await _db.Players
                .Include(p => p.Position)
                .FirstOrDefaultAsync(p => p.PlayerId == playerId, ct)
                ?? throw new InvalidOperationException("Jogador não encontrado.");

            if (roundLimits.OverallMin is int overallMin && player.Overall < overallMin)
            {
                throw new InvalidOperationException($"❌ Este jogador está abaixo do overall mínimo ({overallMin}) permitido nesta rodada.");
            }

            if (roundLimits.OverallMax is int overallMax && player.Overall > overallMax)
            {
                throw new InvalidOperationException($"❌ Este jogador excede o overall máximo ({overallMax}) permitido nesta rodada.");
            }

            var fromTeam = await AplicarEscolhaAsync(draft, currentPick, player, ct);
            currentPick.Automatica = false;

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new EscolhaRegistrada(currentPick, player, fromTeam?.TeamName, false);
        });
    }

    /// <summary>Vincula o jogador ao time da escolha. Roda dentro da transação da escolha; não salva.</summary>
    private async Task<Team?> AplicarEscolhaAsync(Draft draft, DraftPick currentPick, Player player, CancellationToken ct)
    {
        Team? fromTeam = null;
        if (draft.Tipo == DraftTipo.Expansao)
        {
            fromTeam = await _expansao.ExecutarEscolhaAsync(draft, currentPick, player, ct);
        }
        else
        {
            var alreadyInRoster = await _db.TeamRosters.AnyAsync(r => r.PlayerId == player.PlayerId, ct);
            if (alreadyInRoster)
            {
                throw new InvalidOperationException("❌ Este jogador já está vinculado a um time.");
            }

            player.CurrentTeamId = currentPick.TeamId;
            _db.TeamRosters.Add(new TeamRoster
            {
                TeamId = currentPick.TeamId,
                PlayerId = player.PlayerId
            });
        }

        currentPick.PlayerId = player.PlayerId;
        currentPick.PickedAtUtc = DateTime.UtcNow;
        return fromTeam;
    }

    /// <summary>Enquanto o time da vez tiver escolha automática com alguém disponível na lista, escolhe por ele.</summary>
    private async Task<List<EscolhaRegistrada>> ExecutarAutomaticasAsync(CancellationToken ct)
    {
        var feitas = new List<EscolhaRegistrada>();

        for (var i = 0; i < MaxEscolhasAutomaticasSeguidas; i++)
        {
            EscolhaRegistrada? escolha;
            try
            {
                escolha = await TentarEscolhaAutomaticaAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // O que já foi salvo fica; se uma automática falhar, o draft para e espera o time da vez.
                _logger.LogError(ex, "Falha na escolha automática do draft; a vez fica com o time.");
                _db.ChangeTracker.Clear();
                break;
            }

            if (escolha is null)
            {
                break;
            }

            feitas.Add(escolha);
        }

        return feitas;
    }

    private async Task<EscolhaRegistrada?> TentarEscolhaAutomaticaAsync(CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            var draft = await _db.Drafts
                .OrderByDescending(d => d.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);

            if (draft is null || (draft.Tipo == DraftTipo.Expansao && draft.ProtecaoEncerradaEm is null))
            {
                return null;
            }

            var currentPick = await _db.DraftPicks
                .Include(p => p.Team)
                .Where(p => p.DraftId == draft.DraftId && p.PlayerId == null)
                .OrderBy(p => p.OverallPick)
                .FirstOrDefaultAsync(ct);

            if (currentPick is null)
            {
                return null;
            }

            var config = await _db.DraftAutoPicks
                .AsNoTracking()
                .Where(c => c.DraftId == draft.DraftId && c.TeamId == currentPick.TeamId && c.Ativo)
                .Select(c => new { c.Modo })
                .FirstOrDefaultAsync(ct);

            if (config is null)
            {
                return null;
            }

            short? listaPositionId = null;
            if (config.Modo == DraftAutoPickModo.Posicao)
            {
                listaPositionId = await _db.DraftAutoPickRodadas
                    .AsNoTracking()
                    .Where(r => r.DraftId == draft.DraftId && r.TeamId == currentPick.TeamId && r.RoundNumber == currentPick.RoundNumber)
                    .Select(r => (short?)r.PositionId)
                    .FirstOrDefaultAsync(ct);

                if (listaPositionId is null)
                {
                    return null;
                }
            }

            var candidatos = await _db.DraftAutoPickItens
                .AsNoTracking()
                .Where(i => i.DraftId == draft.DraftId && i.TeamId == currentPick.TeamId && i.PositionId == listaPositionId)
                .OrderBy(i => i.Ordem)
                .Select(i => i.PlayerId)
                .ToListAsync(ct);

            if (candidatos.Count == 0)
            {
                return null;
            }

            var disponiveis = await (await ConsultaDisponiveisAsync(draft, currentPick.RoundNumber, ct))
                .Where(p => candidatos.Contains(p.PlayerId))
                .Select(p => p.PlayerId)
                .ToListAsync(ct);

            var escolhidoId = candidatos.FirstOrDefault(disponiveis.Contains);
            if (escolhidoId == 0)
            {
                // Lista esgotada: o time escolhe manualmente.
                return null;
            }

            var player = await _db.Players
                .Include(p => p.Position)
                .FirstAsync(p => p.PlayerId == escolhidoId, ct);

            var fromTeam = await AplicarEscolhaAsync(draft, currentPick, player, ct);
            currentPick.Automatica = true;

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new EscolhaRegistrada(currentPick, player, fromTeam?.TeamName, true);
        });
    }

    private async Task<DraftPickResultDto> FinalizarSequenciaAsync(IReadOnlyList<EscolhaRegistrada> escolhas, CancellationToken ct)
    {
        var state = await GetStateAsync(ct);
        var selection = BuildSelectionInfo(escolhas, state);

        try
        {
            await _hubContext.Clients.All.SendAsync("DraftAtualizado", cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar notificação de atualização do draft.");
        }

        var resumo = escolhas
            .Select(e => new DraftEscolhaResumoDto(
                e.Pick.RoundNumber,
                e.Pick.PickInRound,
                e.Pick.OverallPick,
                e.Pick.Team.TeamName,
                PlayerLabel(e),
                e.Player.Position.Name,
                e.Automatica))
            .ToList();

        return new DraftPickResultDto(state, selection, resumo);
    }

    // Draft de expansão: mostra de onde o jogador saiu.
    private static string PlayerLabel(EscolhaRegistrada escolha)
        => escolha.FromTeam is null ? escolha.Player.Name : $"{escolha.Player.Name} ({escolha.FromTeam})";

    private DraftPickSelectionDto BuildSelectionInfo(IReadOnlyList<EscolhaRegistrada> escolhas, DraftStateDto state)
    {
        var pick = escolhas[0].Pick;
        var player = escolhas[0].Player;

        var nextTeamName = state.DraftCompleted
            ? "Draft concluído"
            : string.IsNullOrWhiteSpace(state.CurrentTeamName)
                ? "A definir"
                : state.CurrentTeamName!;

        var message = BuildWhatsappMessage(escolhas, nextTeamName);
        var shareUrl = $"https://wa.me/?text={Uri.EscapeDataString(message)}";

        var groupLink = string.IsNullOrWhiteSpace(_securityOptions.WhatsappGroupLink)
            ? null
            : _securityOptions.WhatsappGroupLink;

        return new DraftPickSelectionDto(
            pick.DraftId,
            pick.RoundNumber,
            pick.PickInRound,
            pick.OverallPick,
            pick.TeamId,
            pick.Team.TeamName,
            pick.Team.OwnerName,
            player.PlayerId,
            player.Name,
            player.PositionId,
            player.Position.Name,
            message,
            shareUrl,
            nextTeamName,
            groupLink);
    }

    /// <summary>Uma mensagem só para a sequência: a escolha que a disparou e as automáticas que vieram atrás.</summary>
    private static string BuildWhatsappMessage(IReadOnlyList<EscolhaRegistrada> escolhas, string nextTeam)
    {
        if (escolhas.Count == 1 && !escolhas[0].Automatica)
        {
            var e = escolhas[0];
            return BuildWhatsappMessage(e.Pick.Team.TeamName, PlayerLabel(e), e.Pick.PickInRound, e.Pick.RoundNumber, nextTeam);
        }

        var linhas = escolhas.Select(e =>
            $"{(e.Automatica ? "🤖 " : "")}{e.Pick.Team.TeamName} escolheu {PlayerLabel(e)}"
            + $"{(e.Automatica ? " (automática)" : "")} com a escolha {e.Pick.PickInRound} da rodada {e.Pick.RoundNumber}!");

        return string.Join("\n", linhas) + $"\nPróximo a escolher: {nextTeam}.";
    }

    private static string BuildWhatsappMessage(string team, string player, int pick, int round, string nextTeam)
        => $"{team} escolheu {player} com a escolha {pick} da rodada {round}! Próximo a escolher: {nextTeam}.";

    public static string BuildWhatsappUrl(string team, string player, int pick, int round, string nextTeam)
    {
        var message = BuildWhatsappMessage(team, player, pick, round, nextTeam);
        return $"https://wa.me/?text={Uri.EscapeDataString(message)}";
    }
}
