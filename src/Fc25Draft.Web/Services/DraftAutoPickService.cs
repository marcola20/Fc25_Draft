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
/// <remarks>
/// Um time com escolhas no draft em andamento edita a lista daquele draft. Um time fora dele monta a
/// lista do próximo draft (a "prévia"), com as rodadas planejadas pelo admin; quando o draft é gerado,
/// as prévias viram listas do draft e as escolhas automáticas já começam.
/// </remarks>
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
        var team = await ResolverTimeAsync(token, ct);
        var draft = await GetDraftDoTimeAsync(team.TeamId, ct);
        return draft is not null
            ? await MontarAsync(draft, team.TeamId, team.TeamName, ct)
            : await MontarPreviaAsync(await GetPlanoObrigatorioAsync(ct), team.TeamId, team.TeamName, ct);
    }

    public async Task<DraftAutoPickSaveResultDto> SalvarAsync(string? token, DraftAutoPickSaveRequestDto request, CancellationToken ct = default)
    {
        var team = await ResolverTimeAsync(token, ct);
        var draft = await GetDraftDoTimeAsync(team.TeamId, ct);

        if (draft is null)
        {
            var plano = await GetPlanoObrigatorioAsync(ct);
            var (itensPrevia, rodadasPrevia) = await ValidarAsync(request, plano.Select(r => r.Round).ToList(), ct);
            await SalvarPreviaAsync(team.TeamId, request, itensPrevia, rodadasPrevia, ct);
            return new DraftAutoPickSaveResultDto(await MontarPreviaAsync(plano, team.TeamId, team.TeamName, ct), null);
        }

        var rodadasRestantes = await _db.DraftPicks
            .AsNoTracking()
            .Where(p => p.DraftId == draft.DraftId && p.TeamId == team.TeamId && p.PlayerId == null)
            .Select(p => p.RoundNumber)
            .Distinct()
            .ToListAsync(ct);

        var (itens, rodadas) = await ValidarAsync(request, rodadasRestantes, ct);

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            // No Blazor Server o contexto vive o circuito inteiro: as linhas do salvamento anterior
            // continuam rastreadas e o ExecuteDelete não as solta. Sem isso, salvar de novo a mesma
            // rodada dá conflito de chave.
            Soltar(e => e is DraftAutoPickRodada r && r.DraftId == draft.DraftId && r.TeamId == team.TeamId
                        || e is DraftAutoPickItem i && i.DraftId == draft.DraftId && i.TeamId == team.TeamId);

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

    // ── Próximo draft ────────────────────────────────────────────────────────

    /// <summary>Rodadas planejadas para o próximo draft (vazio se o admin ainda não planejou).</summary>
    public async Task<IReadOnlyList<DraftPlanoRodadaDto>> GetPlanoAsync(CancellationToken ct = default) =>
        await _db.DraftPlanejadoRodadas
            .AsNoTracking()
            .OrderBy(r => r.RoundNumber)
            .Select(r => new DraftPlanoRodadaDto(r.RoundNumber, r.OverallMin, r.OverallMax))
            .ToListAsync(ct);

    /// <summary>Define as rodadas do próximo draft. Tirar rodadas remove a posição escolhida nelas nas prévias.</summary>
    public async Task SalvarPlanoAsync(IReadOnlyList<DraftPlanoRodadaDto> rodadas, CancellationToken ct = default)
    {
        if (rodadas.Count > 50)
            throw new InvalidOperationException("O draft pode ter no máximo 50 rodadas.");

        foreach (var r in rodadas)
        {
            if (r.OverallMin is int min && r.OverallMax is int max && min > max)
                throw new InvalidOperationException($"Na rodada {r.Round}, o overall mínimo não pode ser maior que o máximo.");
        }

        var numeros = rodadas.Select((_, i) => i + 1).ToList();

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            Soltar(e => e is DraftPlanejadoRodada);
            await _db.DraftPlanejadoRodadas.ExecuteDeleteAsync(ct);
            await _db.DraftAutoPickPreviaRodadas.Where(r => !numeros.Contains(r.RoundNumber)).ExecuteDeleteAsync(ct);

            // Renumera 1..N na ordem recebida.
            _db.DraftPlanejadoRodadas.AddRange(rodadas.Select((r, i) => new DraftPlanejadoRodada
            {
                RoundNumber = i + 1,
                OverallMin = r.OverallMin,
                OverallMax = r.OverallMax
            }));

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }

    /// <summary>
    /// Chamado ao gerar um draft: as prévias dos times que estão nele viram listas do draft.
    /// Depois o planejamento e todas as prévias são apagados (valiam para este draft).
    /// </summary>
    public async Task<int> AplicarPreviasAsync(Guid draftId, CancellationToken ct = default)
    {
        var timesNoDraft = await _db.DraftPicks
            .AsNoTracking()
            .Where(p => p.DraftId == draftId)
            .Select(p => p.TeamId)
            .Distinct()
            .ToListAsync(ct);

        var rodadasDoDraft = await _db.DraftRounds
            .AsNoTracking()
            .Where(r => r.DraftId == draftId)
            .Select(r => r.RoundNumber)
            .ToListAsync(ct);

        var previas = await _db.DraftAutoPickPrevias
            .AsNoTracking()
            .Where(p => timesNoDraft.Contains(p.TeamId))
            .Include(p => p.Rodadas)
            .Include(p => p.Itens)
            .ToListAsync(ct);

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            foreach (var previa in previas)
            {
                _db.DraftAutoPicks.Add(new DraftAutoPick
                {
                    DraftId = draftId,
                    TeamId = previa.TeamId,
                    Modo = previa.Modo,
                    Ativo = previa.Ativo,
                    AtualizadoEm = previa.AtualizadoEm
                });

                _db.DraftAutoPickRodadas.AddRange(previa.Rodadas
                    .Where(r => rodadasDoDraft.Contains(r.RoundNumber))
                    .Select(r => new DraftAutoPickRodada
                    {
                        DraftId = draftId,
                        TeamId = previa.TeamId,
                        RoundNumber = r.RoundNumber,
                        PositionId = r.PositionId
                    }));

                _db.DraftAutoPickItens.AddRange(previa.Itens.Select(i => new DraftAutoPickItem
                {
                    DraftAutoPickItemId = Guid.NewGuid(),
                    DraftId = draftId,
                    TeamId = previa.TeamId,
                    PositionId = i.PositionId,
                    PlayerId = i.PlayerId,
                    Ordem = i.Ordem
                }));
            }

            await _db.SaveChangesAsync(ct);

            Soltar(e => e is DraftAutoPickPrevia or DraftAutoPickPreviaRodada or DraftAutoPickPreviaItem or DraftPlanejadoRodada);
            await _db.DraftAutoPickPreviaItens.ExecuteDeleteAsync(ct);
            await _db.DraftAutoPickPreviaRodadas.ExecuteDeleteAsync(ct);
            await _db.DraftAutoPickPrevias.ExecuteDeleteAsync(ct);
            await _db.DraftPlanejadoRodadas.ExecuteDeleteAsync(ct);

            await transaction.CommitAsync(ct);
        });

        return previas.Count(p => p.Ativo);
    }

    private async Task SalvarPreviaAsync(
        Guid teamId,
        DraftAutoPickSaveRequestDto request,
        List<(short? PositionId, int PlayerId, int Ordem)> itens,
        List<DraftAutoPickRodadaDto> rodadas,
        CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            Soltar(e => e is DraftAutoPickPreviaRodada r && r.TeamId == teamId
                        || e is DraftAutoPickPreviaItem i && i.TeamId == teamId);

            await _db.DraftAutoPickPreviaItens.Where(i => i.TeamId == teamId).ExecuteDeleteAsync(ct);
            await _db.DraftAutoPickPreviaRodadas.Where(r => r.TeamId == teamId).ExecuteDeleteAsync(ct);

            var previa = await _db.DraftAutoPickPrevias.FirstOrDefaultAsync(p => p.TeamId == teamId, ct);
            if (previa is null)
            {
                previa = new DraftAutoPickPrevia { TeamId = teamId };
                _db.DraftAutoPickPrevias.Add(previa);
            }

            previa.Modo = request.Modo;
            previa.Ativo = request.Ativo;
            previa.AtualizadoEm = DateTime.UtcNow;

            _db.DraftAutoPickPreviaRodadas.AddRange(rodadas.Select(r => new DraftAutoPickPreviaRodada
            {
                TeamId = teamId,
                RoundNumber = r.Round,
                PositionId = r.PositionId
            }));

            _db.DraftAutoPickPreviaItens.AddRange(itens.Select(i => new DraftAutoPickPreviaItem
            {
                DraftAutoPickPreviaItemId = Guid.NewGuid(),
                TeamId = teamId,
                PositionId = i.PositionId,
                PlayerId = i.PlayerId,
                Ordem = i.Ordem
            }));

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }

    // ── Validação e montagem ─────────────────────────────────────────────────

    /// <summary>Confere a lista enviada e devolve os itens e as rodadas a gravar.</summary>
    private async Task<(List<(short? PositionId, int PlayerId, int Ordem)> Itens, List<DraftAutoPickRodadaDto> Rodadas)> ValidarAsync(
        DraftAutoPickSaveRequestDto request, IReadOnlyCollection<int> rodadasValidas, CancellationToken ct)
    {
        if (rodadasValidas.Count == 0)
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

            var foraDoTime = rodadas.Where(r => !rodadasValidas.Contains(r.Round)).Select(r => r.Round).ToList();
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

        return (itens, rodadas);
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
            .Select(i => new ItemFlat(i.PositionId, i.Ordem, i.PlayerId, i.Player.Name, i.Player.PositionId,
                i.Player.Position.Name, i.Player.Overall, i.Player.Age))
            .ToListAsync(ct);

        return new DraftAutoPickDto(
            teamId,
            teamName,
            config is not null,
            config?.Modo ?? DraftAutoPickModo.Jogador,
            config?.Ativo ?? false,
            config?.AtualizadoEm,
            minhasRodadas,
            await JogadoresAsync(itens, proximoDraft: false, ct));
    }

    private async Task<DraftAutoPickDto> MontarPreviaAsync(IReadOnlyList<DraftPlanoRodadaDto> plano, Guid teamId, string teamName, CancellationToken ct)
    {
        var previa = await _db.DraftAutoPickPrevias
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TeamId == teamId, ct);

        var posicaoPorRodada = await _db.DraftAutoPickPreviaRodadas
            .AsNoTracking()
            .Where(r => r.TeamId == teamId)
            .ToDictionaryAsync(r => r.RoundNumber, r => r.PositionId, ct);

        var rodadas = plano
            .Select(r => new DraftAutoPickRodadaStatusDto(r.Round, 0, r.OverallMin, r.OverallMax,
                posicaoPorRodada.TryGetValue(r.Round, out var pos) ? pos : null))
            .ToList();

        var itens = await _db.DraftAutoPickPreviaItens
            .AsNoTracking()
            .Where(i => i.TeamId == teamId)
            .Select(i => new ItemFlat(i.PositionId, i.Ordem, i.PlayerId, i.Player.Name, i.Player.PositionId,
                i.Player.Position.Name, i.Player.Overall, i.Player.Age))
            .ToListAsync(ct);

        return new DraftAutoPickDto(
            teamId,
            teamName,
            previa is not null,
            previa?.Modo ?? DraftAutoPickModo.Jogador,
            previa?.Ativo ?? false,
            previa?.AtualizadoEm,
            rodadas,
            await JogadoresAsync(itens, proximoDraft: true, ct),
            ProximoDraft: true);
    }

    private sealed record ItemFlat(short? ListaPositionId, int Ordem, int PlayerId, string Nome, short PositionId,
        string PositionName, int Overall, int? Age);

    // No próximo draft vale quem está sem time; no draft em andamento, as regras dele (ex.: expansão).
    private async Task<List<DraftAutoPickJogadorDto>> JogadoresAsync(List<ItemFlat> itens, bool proximoDraft, CancellationToken ct)
    {
        var ids = itens.Select(i => i.PlayerId).ToList();
        var disponiveis = proximoDraft
            ? await _draftState.FiltrarLivresAsync(ids, ct)
            : await _draftState.FiltrarDisponiveisAsync(ids, ct);

        return itens
            .OrderBy(i => i.ListaPositionId)
            .ThenBy(i => i.Ordem)
            .Select(i => new DraftAutoPickJogadorDto(i.ListaPositionId, i.Ordem, i.PlayerId, i.Nome, i.PositionId,
                i.PositionName, i.Overall, i.Age, disponiveis.Contains(i.PlayerId)))
            .ToList();
    }

    /// <summary>O draft em andamento, se o time ainda tem escolhas nele; senão nulo (vale o próximo draft).</summary>
    private async Task<Draft?> GetDraftDoTimeAsync(Guid teamId, CancellationToken ct)
    {
        var draft = await _db.Drafts
            .AsNoTracking()
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (draft is null)
            return null;

        var temEscolha = await _db.DraftPicks
            .AnyAsync(p => p.DraftId == draft.DraftId && p.TeamId == teamId && p.PlayerId == null, ct);

        return temEscolha ? draft : null;
    }

    private async Task<IReadOnlyList<DraftPlanoRodadaDto>> GetPlanoObrigatorioAsync(CancellationToken ct)
    {
        var plano = await GetPlanoAsync(ct);
        if (plano.Count == 0)
            throw new InvalidOperationException(
                "Seu time não tem escolhas no draft atual, e o admin ainda não planejou o próximo draft. Assim que ele planejar, você já pode montar a lista.");
        return plano;
    }

    private void Soltar(Func<object, bool> filtro)
    {
        foreach (var entry in _db.ChangeTracker.Entries().Where(e => filtro(e.Entity)).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private async Task<(Guid TeamId, string TeamName)> ResolverTimeAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Informe o token do time.");

        var acesso = await _db.AcessoPorTokenAsync(token, ct)
            ?? throw new InvalidOperationException("Token não pertence a nenhum time. A escolha automática é configurada com o token do treinador.");

        return (acesso.TimeId, acesso.TimeNome);
    }
}
