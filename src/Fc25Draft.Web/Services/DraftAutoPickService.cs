using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Services;

/// <summary>
/// Listas da escolha automática de cada time. Quem executa as escolhas é o
/// <see cref="DraftStateService"/>, sempre que a vez passa para um time com lista ativa.
/// </summary>
public class DraftAutoPickService
{
    private readonly DraftDbContext _db;
    private readonly DraftStateService _draftState;

    public DraftAutoPickService(DraftDbContext db, DraftStateService draftState)
    {
        _db = db;
        _draftState = draftState;
    }

    public async Task<DraftAutoPickDto> GetAsync(string? token, CancellationToken ct = default)
    {
        var draft = await GetDraftAtualAsync(ct);
        var team = await ResolverTimeAsync(token, ct);
        return await MontarAsync(draft, team.TeamId, team.TeamName, ct);
    }

    public async Task<DraftAutoPickSaveResultDto> SalvarAsync(string? token, DraftAutoPickSaveRequestDto request, CancellationToken ct = default)
    {
        var draft = await GetDraftAtualAsync(ct);
        var team = await ResolverTimeAsync(token, ct);

        var rodadasRestantes = await _db.DraftPicks
            .AsNoTracking()
            .Where(p => p.DraftId == draft.DraftId && p.TeamId == team.TeamId && p.PlayerId == null)
            .Select(p => p.RoundNumber)
            .Distinct()
            .ToListAsync(ct);

        if (rodadasRestantes.Count == 0)
            throw new InvalidOperationException("Seu time não tem mais escolhas neste draft.");

        var itens = new List<(short? PositionId, int PlayerId, int Ordem)>();
        var rodadas = new List<DraftAutoPickRodadaDto>();

        if (request.Modo == DraftAutoPickModo.Jogador)
        {
            var ids = (request.Jogadores ?? Array.Empty<int>()).Distinct().ToList();
            if (request.Ativo && ids.Count == 0)
                throw new InvalidOperationException("Adicione pelo menos 1 jogador à lista.");

            itens.AddRange(ids.Select((id, i) => ((short?)null, id, i + 1)));
        }
        else
        {
            rodadas = (request.Rodadas ?? Array.Empty<DraftAutoPickRodadaDto>())
                .GroupBy(r => r.Round)
                .Select(g => g.Last())
                .ToList();

            var foraDoTime = rodadas.Where(r => !rodadasRestantes.Contains(r.Round)).Select(r => r.Round).ToList();
            if (foraDoTime.Count > 0)
                throw new InvalidOperationException($"Seu time não tem escolha pendente na(s) rodada(s) {string.Join(", ", foraDoTime)}.");

            var listas = (request.Listas ?? Array.Empty<DraftAutoPickListaPosicaoDto>())
                .GroupBy(l => l.PositionId)
                .ToDictionary(g => g.Key, g => g.SelectMany(l => l.PlayerIds).Distinct().ToList());

            if (request.Ativo && rodadas.Count == 0)
                throw new InvalidOperationException("Escolha a posição de pelo menos 1 rodada.");

            var semLista = rodadas
                .Where(r => !listas.TryGetValue(r.PositionId, out var l) || l.Count == 0)
                .Select(r => r.Round)
                .ToList();
            if (request.Ativo && semLista.Count > 0)
                throw new InvalidOperationException($"Monte a lista da posição escolhida na(s) rodada(s) {string.Join(", ", semLista)}.");

            // Só guarda as listas das posições que alguma rodada usa.
            var posicoesUsadas = rodadas.Select(r => r.PositionId).ToHashSet();
            foreach (var (positionId, playerIds) in listas.Where(l => posicoesUsadas.Contains(l.Key)))
            {
                itens.AddRange(playerIds.Select((id, i) => ((short?)positionId, id, i + 1)));
            }
        }

        var todosIds = itens.Select(i => i.PlayerId).ToList();
        var posicaoDoJogador = await _db.Players
            .AsNoTracking()
            .Where(p => todosIds.Contains(p.PlayerId))
            .Select(p => new { p.PlayerId, p.PositionId, p.Name })
            .ToDictionaryAsync(p => p.PlayerId, ct);

        if (posicaoDoJogador.Count != todosIds.Distinct().Count())
            throw new InvalidOperationException("Há jogadores inválidos na lista.");

        if (todosIds.Count != todosIds.Distinct().Count())
            throw new InvalidOperationException("Um jogador não pode estar em mais de uma lista.");

        var posicaoErrada = itens.FirstOrDefault(i => i.PositionId is short pos && posicaoDoJogador[i.PlayerId].PositionId != pos);
        if (posicaoErrada.PlayerId != 0)
            throw new InvalidOperationException($"{posicaoDoJogador[posicaoErrada.PlayerId].Name} não é da posição da lista em que foi colocado.");

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            // No Blazor Server o contexto vive o circuito inteiro: as linhas do salvamento anterior
            // continuam rastreadas e o ExecuteDelete não as solta. Sem isso, salvar de novo a mesma
            // rodada dá conflito de chave.
            foreach (var entry in _db.ChangeTracker.Entries()
                         .Where(e => e.Entity is DraftAutoPickRodada r && r.DraftId == draft.DraftId && r.TeamId == team.TeamId
                                  || e.Entity is DraftAutoPickItem i && i.DraftId == draft.DraftId && i.TeamId == team.TeamId)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }

            await _db.DraftAutoPickItens
                .Where(i => i.DraftId == draft.DraftId && i.TeamId == team.TeamId)
                .ExecuteDeleteAsync(ct);
            await _db.DraftAutoPickRodadas
                .Where(r => r.DraftId == draft.DraftId && r.TeamId == team.TeamId)
                .ExecuteDeleteAsync(ct);

            var config = await _db.DraftAutoPicks
                .FirstOrDefaultAsync(c => c.DraftId == draft.DraftId && c.TeamId == team.TeamId, ct);
            if (config is null)
            {
                config = new DraftAutoPick { DraftId = draft.DraftId, TeamId = team.TeamId };
                _db.DraftAutoPicks.Add(config);
            }

            config.Modo = request.Modo;
            config.Ativo = request.Ativo;
            config.AtualizadoEm = DateTime.UtcNow;

            _db.DraftAutoPickRodadas.AddRange(rodadas.Select(r => new DraftAutoPickRodada
            {
                DraftId = draft.DraftId,
                TeamId = team.TeamId,
                RoundNumber = r.Round,
                PositionId = r.PositionId
            }));

            _db.DraftAutoPickItens.AddRange(itens.Select(i => new DraftAutoPickItem
            {
                DraftAutoPickItemId = Guid.NewGuid(),
                DraftId = draft.DraftId,
                TeamId = team.TeamId,
                PositionId = i.PositionId,
                PlayerId = i.PlayerId,
                Ordem = i.Ordem
            }));

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });

        // Se já era a vez do time, a lista entra em ação na hora.
        var escolhas = request.Ativo ? await _draftState.ProcessarAutomaticasAsync(ct) : null;

        return new DraftAutoPickSaveResultDto(await MontarAsync(draft, team.TeamId, team.TeamName, ct), escolhas);
    }

    private async Task<DraftAutoPickDto> MontarAsync(Draft draft, Guid teamId, string teamName, CancellationToken ct)
    {
        var config = await _db.DraftAutoPicks
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.DraftId == draft.DraftId && c.TeamId == teamId, ct);

        var posicaoPorRodada = await _db.DraftAutoPickRodadas
            .AsNoTracking()
            .Where(r => r.DraftId == draft.DraftId && r.TeamId == teamId)
            .ToDictionaryAsync(r => r.RoundNumber, r => r.PositionId, ct);

        var minhasRodadas = (await _db.DraftPicks
                .AsNoTracking()
                .Where(p => p.DraftId == draft.DraftId && p.TeamId == teamId && p.PlayerId == null)
                .OrderBy(p => p.OverallPick)
                .Select(p => new { p.RoundNumber, p.OverallPick, p.Round.OverallMin, p.Round.OverallMax })
                .ToListAsync(ct))
            .GroupBy(p => p.RoundNumber)
            .Select(g => g.First())
            .Select(p => new DraftAutoPickRodadaStatusDto(
                p.RoundNumber,
                p.OverallPick,
                p.OverallMin,
                p.OverallMax,
                posicaoPorRodada.TryGetValue(p.RoundNumber, out var pos) ? pos : null))
            .ToList();

        var itens = await _db.DraftAutoPickItens
            .AsNoTracking()
            .Where(i => i.DraftId == draft.DraftId && i.TeamId == teamId)
            .OrderBy(i => i.PositionId)
            .ThenBy(i => i.Ordem)
            .Select(i => new
            {
                i.PositionId,
                i.Ordem,
                i.PlayerId,
                i.Player.Name,
                PlayerPositionId = i.Player.PositionId,
                PositionName = i.Player.Position.Name,
                i.Player.Overall,
                i.Player.Age
            })
            .ToListAsync(ct);

        var disponiveis = await _draftState.FiltrarDisponiveisAsync(itens.Select(i => i.PlayerId).ToList(), ct);

        var jogadores = itens
            .Select(i => new DraftAutoPickJogadorDto(
                i.PositionId,
                i.Ordem,
                i.PlayerId,
                i.Name,
                i.PlayerPositionId,
                i.PositionName,
                i.Overall,
                i.Age,
                disponiveis.Contains(i.PlayerId)))
            .ToList();

        return new DraftAutoPickDto(
            teamId,
            teamName,
            config is not null,
            config?.Modo ?? DraftAutoPickModo.Jogador,
            config?.Ativo ?? false,
            config?.AtualizadoEm,
            minhasRodadas,
            jogadores);
    }

    private async Task<Draft> GetDraftAtualAsync(CancellationToken ct)
    {
        var draft = await _db.Drafts
            .AsNoTracking()
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Nenhum draft encontrado.");

        var pendente = await _db.DraftPicks.AnyAsync(p => p.DraftId == draft.DraftId && p.PlayerId == null, ct);
        if (!pendente)
            throw new InvalidOperationException("O draft atual já foi concluído.");

        return draft;
    }

    private async Task<(Guid TeamId, string TeamName)> ResolverTimeAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Informe o token do time.");

        var normalizado = token.Trim();
        var time = await _db.Teams
            .AsNoTracking()
            .Where(t => t.Token == normalizado || t.AuxToken == normalizado)
            .Select(t => new { t.TeamId, t.TeamName })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Token não pertence a nenhum time. A escolha automática é configurada com o token do time.");

        return (time.TeamId, time.TeamName);
    }
}
