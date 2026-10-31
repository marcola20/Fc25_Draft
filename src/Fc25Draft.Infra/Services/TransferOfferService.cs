using System.Globalization;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class TransferOfferService : ITransferOfferService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _timeProvider;
    private static readonly CultureInfo BrCulture = CultureInfo.GetCultureInfo("pt-BR");

    public TransferOfferService(DraftDbContext db, TimeProvider? timeProvider = null)
    {
        _db = db;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<TransferOfferListItemDto> CreateOfferAsync(CreateTransferOfferDto dto, CancellationToken ct)
    {
        if (dto.FromTeamId == Guid.Empty) throw new ArgumentException("Time de origem inválido.");
        if (dto.ToTeamId == Guid.Empty) throw new ArgumentException("Time de destino inválido.");
        if (dto.FromTeamId == dto.ToTeamId) throw new ArgumentException("Os times devem ser diferentes.");
        var distinctTargets = (dto.TargetPlayerIds ?? Array.Empty<Guid>()).Distinct().ToArray();
        var distinctOffered = (dto.OfferedPlayerIds ?? Array.Empty<Guid>()).Distinct().ToArray();
        var targetPicks = (dto.TargetPicks ?? []).Distinct().ToArray();
        var offeredPicks = (dto.OfferedPicks ?? []).Distinct().ToArray();
        // Negócio só de picks (e dinheiro): vale fora da janela e não conta no limite de transferências.
        var soPicks = distinctTargets.Length == 0 && distinctOffered.Length == 0;

        if (soPicks && targetPicks.Length == 0 && offeredPicks.Length == 0)
            throw new ArgumentException("Selecione ao menos um jogador ou uma pick do draft na negociação.");

        if (dto.Type == OfferType.Swap && distinctOffered.Length == 0 && offeredPicks.Length == 0)
            throw new ArgumentException("Selecione ao menos um jogador ou pick para enviar na troca.");

        if (dto.Type == OfferType.Loan && (targetPicks.Length > 0 || offeredPicks.Length > 0))
            throw new ArgumentException("Empréstimo não leva pick do draft.");

        if (dto.Money > 0 && dto.MoneyPayerTeamId is null)
            throw new ArgumentException("Informe qual time paga o valor em dinheiro.");

        if (dto.MoneyPayerTeamId.HasValue &&
            dto.MoneyPayerTeamId.Value != dto.FromTeamId && dto.MoneyPayerTeamId.Value != dto.ToTeamId)
            throw new ArgumentException("O time pagador deve ser um dos times envolvidos.");

        if (dto.Type == OfferType.Loan && distinctTargets.Length > 0 && distinctOffered.Length > 0)
            throw new ArgumentException("No empréstimo os jogadores vão para um time só: peça ou ofereça, não os dois.");

        if (dto.BuyOptionPrice is < 0)
            throw new ArgumentException("A opção de compra não pode ser negativa.");
        var buyOptionPrice = dto.Type == OfferType.Loan && dto.BuyOptionPrice is > 0
            ? decimal.Round(dto.BuyOptionPrice.Value, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;

        // Cláusula de recompra: só em venda de um jogador (o preço é dele), e o direito é de quem vende.
        decimal? recompra = null;
        if (dto.BuybackPrice is > 0)
        {
            if (dto.Type != OfferType.Sale)
                throw new ArgumentException("A cláusula de recompra só vale em venda.");
            if (distinctTargets.Length + distinctOffered.Length != 1)
                throw new ArgumentException("A cláusula de recompra é para a venda de um jogador só.");
            recompra = decimal.Round(dto.BuybackPrice.Value, 2, MidpointRounding.AwayFromZero);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var fromTeam = await _db.Teams.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TeamId == dto.FromTeamId, ct)
            ?? throw new KeyNotFoundException("Time de origem não encontrado.");

        var toTeam = await _db.Teams.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TeamId == dto.ToTeamId, ct)
            ?? throw new KeyNotFoundException("Time de destino não encontrado.");

        var cfg = await _db.TransferConfigs.AsNoTracking().FirstOrDefaultAsync(ct) ?? TransferConfig.Default();
        if (!soPicks)
        {
            EnsureMercadoAberto(cfg);

            if (dto.Type == OfferType.Loan)
            {
                EnsureLoanLimit(cfg, distinctTargets.Length > 0 ? fromTeam : toTeam);
            }
            else if (dto.Type == OfferType.Swap)
            {
                EnsureTransferLimit(cfg, fromTeam);
                EnsureTransferLimit(cfg, toTeam);
            }
            else
            {
                EnsureTransferLimit(cfg, fromTeam);
            }
        }

        // As pedidas são de quem recebe a proposta; as oferecidas, de quem propõe.
        // Proteção: quem cede a pick protege (antes da loteria); vale para pedidas e oferecidas.
        var protecoes = (dto.Protecoes ?? [])
            .Where(pr => targetPicks.Contains(pr.Pick) || offeredPicks.Contains(pr.Pick))
            .GroupBy(pr => pr.Pick)
            .ToDictionary(g => g.Key, g => g.First());
        var agoraBrasilia = HorarioDeBrasilia.Agora(_timeProvider);
        if (await PicksDoDraftService.ProblemaAsync(_db, agoraBrasilia, targetPicks, toTeam.TeamId, toTeam.TeamName, ct, protecoes) is { } erroPedidas)
            throw new InvalidOperationException(erroPedidas);
        if (await PicksDoDraftService.ProblemaAsync(_db, agoraBrasilia, offeredPicks, fromTeam.TeamId, fromTeam.TeamName, ct, protecoes) is { } erroOferecidas)
            throw new InvalidOperationException(erroOferecidas);

        var targetPlayers = await _db.Players
            .Include(p => p.Position)
            .Include(p => p.TeamRosters)
            .Where(p => distinctTargets.Contains(p.PlayerGuid))
            .ToListAsync(ct);

        if (targetPlayers.Count != distinctTargets.Length)
            throw new InvalidOperationException("Um ou mais jogadores alvo não foram encontrados.");

        if (targetPlayers.Any(p => !PlayerBelongsToTeam(p, dto.ToTeamId)))
            throw new InvalidOperationException("Todos os jogadores alvo devem pertencer ao time de destino.");

        var offeredPlayers = new List<Player>();
        if (distinctOffered.Length > 0)
        {
            offeredPlayers = await _db.Players
                .Include(p => p.Position)
                .Include(p => p.TeamRosters)
                .Where(p => distinctOffered.Contains(p.PlayerGuid))
                .ToListAsync(ct);

            if (offeredPlayers.Count != distinctOffered.Length)
                throw new InvalidOperationException("Um ou mais jogadores oferecidos não foram encontrados.");

            if (offeredPlayers.Any(p => !PlayerBelongsToTeam(p, dto.FromTeamId)))
                throw new InvalidOperationException("Todos os jogadores oferecidos devem pertencer ao seu time.");
        }

        await EnsureNotOnLoanAsync(targetPlayers.Concat(offeredPlayers), ct);
        var pediram = await PediramParaSairAsync(targetPlayers.Concat(offeredPlayers), ct);
        if (dto.Type == OfferType.Loan) EnsureNaoEmprestaQuemPediuParaSair(pediram);

        // Os alvos saem do time de destino e os oferecidos saem do time de origem.
        await EnsureElencoAsync(cfg, toTeam, targetPlayers.Count, offeredPlayers.Count, ct,
            targetPlayers.Count(p => pediram.ContainsKey(p.PlayerId)));
        await EnsureElencoAsync(cfg, fromTeam, offeredPlayers.Count, targetPlayers.Count, ct,
            offeredPlayers.Count(p => pediram.ContainsKey(p.PlayerId)));

        if (dto.ParentOfferId.HasValue)
        {
            var parent = await _db.TransferOffers
                .FirstOrDefaultAsync(o => o.OfferId == dto.ParentOfferId.Value, ct)
                ?? throw new KeyNotFoundException("Proposta original não encontrada.");

            if (parent.Status != OfferStatus.Pending)
                throw new InvalidOperationException("Só é possível contraofertar propostas pendentes.");

            // Só quem recebeu a proposta contrapropõe, e para quem a mandou.
            if (parent.ToTeamId != dto.FromTeamId || parent.FromTeamId != dto.ToTeamId)
                throw new InvalidOperationException("Só o time que recebeu a proposta pode fazer a contraproposta, e para quem a mandou.");

            parent.Status = OfferStatus.Countered;
            parent.UpdatedAtUtc = now;
        }

        var offer = new TransferOffer
        {
            OfferId = Guid.NewGuid(),
            FromTeamId = dto.FromTeamId,
            ToTeamId = dto.ToTeamId,
            Type = dto.Type,
            Status = OfferStatus.Pending,
            Money = decimal.Round(dto.Money, 2, MidpointRounding.AwayFromZero),
            MoneyPayerTeamId = dto.Money > 0 ? dto.MoneyPayerTeamId : null,
            SellOnPercentage = decimal.Round(dto.SellOnPercentage, 2, MidpointRounding.AwayFromZero),
            BuyOptionPrice = buyOptionPrice,
            BuybackPrice = recompra,
            Clauses = dto.Clauses?.Trim(),
            Notes = dto.Notes?.Trim(),
            ParentOfferId = dto.ParentOfferId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var offerPlayers = new List<TransferOfferPlayer>();
        foreach (var tp in targetPlayers)
        {
            offerPlayers.Add(new TransferOfferPlayer
            {
                OfferId = offer.OfferId,
                PlayerId = tp.PlayerId,
                IsTarget = true
            });
        }
        foreach (var op in offeredPlayers)
        {
            offerPlayers.Add(new TransferOfferPlayer
            {
                OfferId = offer.OfferId,
                PlayerId = op.PlayerId,
                IsTarget = false
            });
        }

        offer.Players = offerPlayers;
        offer.Picks = targetPicks.Select(pk => (pk, true)).Concat(offeredPicks.Select(pk => (pk, false)))
            .Select(x => new TransferOfferPick
            {
                OfferId = offer.OfferId, Temporada = x.pk.Temporada, Rodada = x.pk.Rodada,
                TimeOriginalId = x.pk.TimeOriginalId, IsTarget = x.Item2,
                ProtegidaAte = protecoes.GetValueOrDefault(x.pk)?.ProtegidaAte,
                Compensacao = protecoes.GetValueOrDefault(x.pk) is { } pr
                    ? decimal.Round(pr.Compensacao, 2, MidpointRounding.AwayFromZero)
                    : null
            })
            .ToList();
        var nomes = await NomesDosTimesAsync(offer.Picks.Select(pk => pk.TimeOriginalId), ct);

        AvisosDoTime.Criar(_db, toTeam.TeamId, AvisosDoTime.Proposta,
            $"{(dto.ParentOfferId.HasValue ? "Contraproposta" : "Proposta")} do {fromTeam.TeamName}: {Resumo(offer, targetPlayers, offeredPlayers, fromTeam.TeamId, nomes)}",
            "/minha-area", now);

        await _db.TransferOffers.AddAsync(offer, ct);
        await _db.SaveChangesAsync(ct);

        return MapToDto(offer, fromTeam, toTeam, targetPlayers, offeredPlayers, nomes);
    }

    public Task<TransferOfferListItemDto> RespondToOfferAsync(Guid offerId, Guid teamId, OfferStatus response, CancellationToken ct)
    {
        if (response is not (OfferStatus.Accepted or OfferStatus.Rejected))
            throw new ArgumentException("Resposta inválida. Use Accepted ou Rejected.");

        return EmTransacaoSerializavelAsync(() => ResponderAsync(offerId, teamId, response, ct), ct);
    }

    /// <summary>
    /// Dinheiro e elencos de dois times mudam juntos. Em Serializable, duas operações ao mesmo tempo sobre
    /// o mesmo time não gastam o mesmo saldo: o banco recusa uma delas, que é refeita (execution strategy)
    /// já enxergando o que a outra gravou.
    /// </summary>
    private async Task<T> EmTransacaoSerializavelAsync<T>(Func<Task<T>> acao, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // Nada do que já estava carregado neste contexto (saldo, elenco) vale para a tentativa.
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var resultado = await acao();
            await tx.CommitAsync(ct);
            return resultado;
        });
    }

    private async Task<TransferOfferListItemDto> ResponderAsync(Guid offerId, Guid teamId, OfferStatus response, CancellationToken ct)
    {
        var offer = await _db.TransferOffers
            .Include(o => o.Players).ThenInclude(p => p.Player).ThenInclude(p => p.Position)
            .Include(o => o.Players).ThenInclude(p => p.Player).ThenInclude(p => p.TeamRosters)
            .Include(o => o.Picks).ThenInclude(p => p.TimeOriginal)
            .Include(o => o.FromTeam)
            .Include(o => o.ToTeam)
            .FirstOrDefaultAsync(o => o.OfferId == offerId, ct)
            ?? throw new KeyNotFoundException("Proposta não encontrada.");
        var soPicks = offer.Players.Count == 0;

        if (offer.ToTeamId != teamId)
            throw new InvalidOperationException("Apenas o time destinatário pode responder a esta proposta.");

        if (offer.Status != OfferStatus.Pending)
            throw new InvalidOperationException("Esta proposta já foi respondida.");

        if (offer.CreatedAtUtc < _timeProvider.GetUtcNow().UtcDateTime.AddHours(-TransferOffer.HorasParaExpirar))
            throw new InvalidOperationException($"Esta proposta expirou: ficou mais de {TransferOffer.HorasParaExpirar} horas sem resposta.");

        if (response == OfferStatus.Accepted && soPicks)
        {
            // Só picks: sem janela nem limite, mas as picks ainda precisam ser de quem as cede.
            await EnsurePicksAsync(offer, ct);
        }
        else if (response == OfferStatus.Accepted)
        {
            var cfg = await _db.TransferConfigs.AsNoTracking().FirstOrDefaultAsync(ct) ?? TransferConfig.Default();
            EnsureMercadoAberto(cfg);
            await EnsurePicksAsync(offer, ct);
            if (offer.Type == OfferType.Loan)
            {
                EnsureLoanLimit(cfg, LoanBorrower(offer) == offer.FromTeamId ? offer.FromTeam : offer.ToTeam);
            }
            else if (offer.Type == OfferType.Swap)
            {
                EnsureTransferLimit(cfg, offer.FromTeam);
                EnsureTransferLimit(cfg, offer.ToTeam);
            }
            else
            {
                EnsureTransferLimit(cfg, offer.FromTeam);
            }

            await EnsureNotOnLoanAsync(offer.Players.Select(p => p.Player), ct);
            if (offer.Type == OfferType.Loan)
                EnsureNaoEmprestaQuemPediuParaSair(await PediramParaSairAsync(offer.Players.Select(p => p.Player), ct));

            // A proposta pode ser antiga: os pedidos ainda precisam ser de quem recebeu e os oferecidos de quem mandou.
            var mudaram = offer.Players
                .Where(p => !PlayerBelongsToTeam(p.Player, p.IsTarget ? offer.ToTeamId : offer.FromTeamId))
                .Select(p => p.Player.Name)
                .ToList();
            if (mudaram.Count > 0)
                throw new InvalidOperationException(
                    $"A proposta não vale mais: {string.Join(", ", mudaram)} já não {(mudaram.Count == 1 ? "é" : "são")} do mesmo time. Recuse-a ou peça uma nova.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        offer.Status = response;
        offer.UpdatedAtUtc = now;

        if (response == OfferStatus.Accepted)
        {
            if (soPicks)
                await ExecutarSoPicksAsync(offer, ct);
            else
                await ExecuteTransferAsync(offer, ct);
            if (offer.BuybackPrice is decimal precoDaRecompra && offer.Players.Count == 1)
                await CriarRecompraAsync(offer, precoDaRecompra, now, ct);

            // As pedidas vão para quem propôs; as oferecidas, para quem recebeu.
            await PicksDoDraftService.TransferirAsync(_db, offer.Picks.Where(p => p.IsTarget), offer.ToTeamId, offer.FromTeamId, now, ct);
            await PicksDoDraftService.TransferirAsync(_db, offer.Picks.Where(p => !p.IsTarget), offer.FromTeamId, offer.ToTeamId, now, ct);
            await CancelConflictingOffersAsync(offer, ct);
        }

        var targetPlayers = offer.Players.Where(p => p.IsTarget).Select(p => p.Player).ToList();
        var offeredPlayers = offer.Players.Where(p => !p.IsTarget).Select(p => p.Player).ToList();

        AvisosDoTime.Criar(_db, offer.FromTeamId,
            response == OfferStatus.Accepted ? AvisosDoTime.PropostaAceita : AvisosDoTime.PropostaRecusada,
            $"O {offer.ToTeam.TeamName} {(response == OfferStatus.Accepted ? "aceitou" : "recusou")} sua proposta: " +
            Resumo(offer, targetPlayers, offeredPlayers, offer.FromTeamId),
            $"/teams/details/{offer.FromTeamId}", now);

        await _db.SaveChangesAsync(ct);

        return MapToDto(offer, offer.FromTeam, offer.ToTeam, targetPlayers, offeredPlayers);
    }

    public async Task<IReadOnlyList<TransferOfferListItemDto>> GetReceivedOffersAsync(Guid teamId, CancellationToken ct)
    {
        var offers = await QueryOffers()
            .Where(o => o.ToTeamId == teamId && o.Status == OfferStatus.Pending)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(ct);

        return offers.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<TransferOfferListItemDto>> GetAllPendingOffersAsync(CancellationToken ct)
    {
        var offers = await QueryOffers()
            .Where(o => o.Status == OfferStatus.Pending)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(ct);

        return offers.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<TransferOfferListItemDto>> GetSentOffersAsync(Guid teamId, CancellationToken ct)
    {
        var offers = await QueryOffers()
            .Where(o => o.FromTeamId == teamId && o.Status == OfferStatus.Pending)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(ct);

        return offers.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<TransferOfferListItemDto>> GetFinishedOffersAsync(Guid teamId, CancellationToken ct)
    {
        var offers = await QueryOffers()
            .Where(o => (o.FromTeamId == teamId || o.ToTeamId == teamId)
                && (o.Status == OfferStatus.Accepted || o.Status == OfferStatus.Rejected || o.Status == OfferStatus.Countered || o.Status == OfferStatus.Cancelled))
            .OrderByDescending(o => o.UpdatedAtUtc)
            .ToListAsync(ct);

        return offers.Select(MapToDto).ToList();
    }

    public async Task<TransferOfferListItemDto?> GetByIdAsync(Guid offerId, CancellationToken ct)
    {
        var offer = await QueryOffers()
            .FirstOrDefaultAsync(o => o.OfferId == offerId, ct);

        return offer is null ? null : MapToDto(offer);
    }

    public async Task CancelarPeloAdminAsync(Guid offerId, string adminToken, string? motivo, CancellationToken ct)
    {
        var admin = await _db.AdminTokens.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Token == adminToken && a.IsActive, ct)
            ?? throw new UnauthorizedAccessException("Token de administrador inválido.");

        var offer = await _db.TransferOffers
            .Include(o => o.FromTeam)
            .Include(o => o.ToTeam)
            .FirstOrDefaultAsync(o => o.OfferId == offerId, ct)
            ?? throw new KeyNotFoundException("Proposta não encontrada.");
        if (offer.Status != OfferStatus.Pending)
            throw new InvalidOperationException("Só é possível cancelar propostas pendentes.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        offer.Status = OfferStatus.Cancelled;
        offer.UpdatedAtUtc = now;

        var porque = string.IsNullOrWhiteSpace(motivo) ? "" : $" Motivo: {motivo.Trim()}";
        AvisosDoTime.Criar(_db, offer.FromTeamId, AvisosDoTime.PropostaCancelada,
            $"A organização cancelou sua proposta ao {offer.ToTeam.TeamName}.{porque}", "/minha-area", now);
        AvisosDoTime.Criar(_db, offer.ToTeamId, AvisosDoTime.PropostaCancelada,
            $"A organização cancelou a proposta do {offer.FromTeam.TeamName}.{porque}", "/minha-area", now);

        _db.AdminActionsLogs.Add(new AdminActionsLog
        {
            ActionId = Guid.NewGuid(),
            ActionType = AdminActionType.CancelOffer,
            PerformedBy = admin.AdminTokenId.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                offerId, fromTeamId = offer.FromTeamId, toTeamId = offer.ToTeamId, reason = motivo?.Trim()
            }),
            CreatedAtUtc = now
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> ExpirarAntigasAsync(CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var limite = now.AddHours(-TransferOffer.HorasParaExpirar);
        var vencidas = await _db.TransferOffers
            .Include(o => o.ToTeam)
            .Where(o => o.Status == OfferStatus.Pending && o.CreatedAtUtc < limite)
            .ToListAsync(ct);

        foreach (var offer in vencidas)
        {
            offer.Status = OfferStatus.Expired;
            offer.UpdatedAtUtc = now;
            AvisosDoTime.Criar(_db, offer.FromTeamId, AvisosDoTime.PropostaExpirada,
                $"Sua proposta ao {offer.ToTeam.TeamName} expirou: ficou {TransferOffer.HorasParaExpirar} horas sem resposta.",
                "/minha-area", now);
        }

        if (vencidas.Count > 0) await _db.SaveChangesAsync(ct);
        return vencidas.Count;
    }

    public async Task<TransferOfferListItemDto> CancelOfferAsync(Guid offerId, Guid teamId, CancellationToken ct)
    {
        var offer = await _db.TransferOffers
            .Include(o => o.Players).ThenInclude(p => p.Player).ThenInclude(p => p.Position)
            .Include(o => o.FromTeam)
            .Include(o => o.ToTeam)
            .FirstOrDefaultAsync(o => o.OfferId == offerId, ct)
            ?? throw new KeyNotFoundException("Proposta não encontrada.");

        if (offer.FromTeamId != teamId)
            throw new InvalidOperationException("Apenas o time que enviou a proposta pode cancelá-la.");

        if (offer.Status != OfferStatus.Pending)
            throw new InvalidOperationException("Só é possível cancelar propostas pendentes.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        offer.Status = OfferStatus.Cancelled;
        offer.UpdatedAtUtc = now;

        var targetPlayers = offer.Players.Where(p => p.IsTarget).Select(p => p.Player).ToList();
        var offeredPlayers = offer.Players.Where(p => !p.IsTarget).Select(p => p.Player).ToList();

        AvisosDoTime.Criar(_db, offer.ToTeamId, AvisosDoTime.PropostaCancelada,
            $"O {offer.FromTeam.TeamName} retirou a proposta: {Resumo(offer, targetPlayers, offeredPlayers, offer.FromTeamId)}",
            "/minha-area", now);

        await _db.SaveChangesAsync(ct);

        return MapToDto(offer, offer.FromTeam, offer.ToTeam, targetPlayers, offeredPlayers);
    }

    public async Task<IReadOnlyList<ListaTransferenciaItemDto>> GetTransferListAsync(CancellationToken ct)
    {
        return await _db.TeamRosters.AsNoTracking()
            .Where(r => r.AskingPrice != null)
            .OrderByDescending(r => r.ListedAtUtc)
            .Select(r => new ListaTransferenciaItemDto(
                r.PlayerId,
                r.Player.PlayerGuid,
                r.Player.Name,
                r.Player.Position.Name,
                r.Player.PositionId,
                r.Player.Overall,
                r.Player.Age,
                r.TeamId,
                r.Team.TeamName,
                r.AskingPrice!.Value,
                r.ListedAtUtc ?? DateTime.MinValue,
                r.SaidaPedidaEm != null,
                r.PromessaQuebrada))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Quem cede jogador não pode ficar abaixo do mínimo de elenco (quem já está abaixo só contrata até
    /// voltar a ele), e quem recebe não pode passar do máximo. Quem pediu para sair sai mesmo assim
    /// (<paramref name="pediramParaSair"/> dos que saem não contam para o mínimo).
    /// </summary>
    private async Task EnsureElencoAsync(TransferConfig cfg, Team team, int saem, int entram, CancellationToken ct,
        int pediramParaSair = 0)
    {
        if (saem == entram) return;

        var elenco = await _db.TeamRosters.CountAsync(r => r.TeamId == team.TeamId, ct);
        var depois = elenco - saem + entram;

        if (entram > saem && depois > cfg.MaxRosterSize)
            throw new InvalidOperationException(
                $"O {team.TeamName} ficaria com {depois} jogadores; o máximo é {cfg.MaxRosterSize}.");

        var minimo = cfg.MinRosterSizeFor(team);
        if (saem - pediramParaSair > entram && depois + pediramParaSair < minimo)
            throw new InvalidOperationException(elenco < minimo
                ? $"O {team.TeamName} está abaixo do mínimo de elenco ({elenco} de {minimo} jogadores): só pode contratar até voltar ao mínimo."
                : $"O {team.TeamName} ficaria com menos de {minimo} jogadores.");
    }

    public async Task SetAskingPriceAsync(Guid teamId, Guid playerGuid, decimal? askingPrice, CancellationToken ct)
    {
        if (askingPrice is <= 0)
            throw new ArgumentException("Informe um preço maior que zero.");

        // Tirar da lista continua liberado com o mercado fechado; anunciar ou mudar o preço, não.
        if (askingPrice is not null)
        {
            var cfg = await _db.TransferConfigs.AsNoTracking().FirstOrDefaultAsync(ct) ?? TransferConfig.Default();
            EnsureMercadoAberto(cfg);
        }

        var roster = await _db.TeamRosters
            .FirstOrDefaultAsync(r => r.TeamId == teamId && r.Player.PlayerGuid == playerGuid, ct)
            ?? throw new KeyNotFoundException("Jogador não encontrado no seu elenco.");

        if (roster.SaidaPedidaEm is not null)
            throw new InvalidOperationException(
                "Ele pediu para sair: o preço é o da venda rápida e ele só sai da lista com a promessa de colocar para jogar.");

        if (askingPrice is not null && await _db.Emprestimos.AnyAsync(e => e.PlayerId == roster.PlayerId && e.Status == EmprestimoStatus.Ativo, ct))
            throw new InvalidOperationException("Jogador emprestado não pode ser colocado à venda.");

        // Anunciar é querer vender: com o elenco no mínimo (ou abaixo), a venda não passaria.
        if (askingPrice is not null && roster.AskingPrice is null)
        {
            var cfg = await _db.TransferConfigs.AsNoTracking().FirstOrDefaultAsync(ct) ?? TransferConfig.Default();
            var team = await _db.Teams.AsNoTracking().FirstAsync(t => t.TeamId == teamId, ct);
            await EnsureElencoAsync(cfg, team, saem: 1, entram: 0, ct);
        }

        if (askingPrice is null)
        {
            roster.AskingPrice = null;
            roster.ListedAtUtc = null;
        }
        else
        {
            // Mudar só o preço mantém a data em que ele entrou na lista.
            roster.ListedAtUtc ??= _timeProvider.GetUtcNow().UtcDateTime;
            roster.AskingPrice = decimal.Round(askingPrice.Value, 2, MidpointRounding.AwayFromZero);
        }

        await _db.SaveChangesAsync(ct);
    }

    public Task<TransferOfferListItemDto> BuyListedPlayerAsync(Guid buyerTeamId, Guid playerGuid, decimal expectedPrice, CancellationToken ct) =>
        EmTransacaoSerializavelAsync(() => ComprarDaListaAsync(buyerTeamId, playerGuid, expectedPrice, ct), ct);

    private async Task<TransferOfferListItemDto> ComprarDaListaAsync(Guid buyerTeamId, Guid playerGuid, decimal expectedPrice, CancellationToken ct)
    {
        var roster = await _db.TeamRosters
            .Include(r => r.Player).ThenInclude(p => p.Position)
            .FirstOrDefaultAsync(r => r.Player.PlayerGuid == playerGuid, ct)
            ?? throw new KeyNotFoundException("Jogador não encontrado.");

        if (roster.AskingPrice is not decimal price)
            throw new InvalidOperationException("Este jogador não está mais à venda.");

        if (roster.TeamId == buyerTeamId)
            throw new InvalidOperationException("O jogador já é do seu time.");

        if (price != decimal.Round(expectedPrice, 2, MidpointRounding.AwayFromZero))
            throw new InvalidOperationException($"O preço mudou para {price.ToString("C", BrCulture)}. Confira antes de comprar.");

        var buyer = await _db.Teams.FirstOrDefaultAsync(t => t.TeamId == buyerTeamId, ct)
            ?? throw new KeyNotFoundException("Time comprador não encontrado.");
        var seller = await _db.Teams.FirstOrDefaultAsync(t => t.TeamId == roster.TeamId, ct)
            ?? throw new KeyNotFoundException("Time vendedor não encontrado.");

        var cfg = await _db.TransferConfigs.AsNoTracking().FirstOrDefaultAsync(ct) ?? TransferConfig.Default();
        EnsureMercadoAberto(cfg);
        EnsureTransferLimit(cfg, buyer);

        // Mesma venda de uma proposta aceita: o comprador propõe o preço pedido e o vendedor já aceitou ao listar.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var offer = new TransferOffer
        {
            OfferId = Guid.NewGuid(),
            FromTeamId = buyer.TeamId,
            ToTeamId = seller.TeamId,
            Type = OfferType.Sale,
            Status = OfferStatus.Accepted,
            Money = price,
            MoneyPayerTeamId = buyer.TeamId,
            Notes = "Compra direta pela lista de transferência.",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        offer.Players.Add(new TransferOfferPlayer
        {
            OfferId = offer.OfferId,
            PlayerId = roster.PlayerId,
            IsTarget = true,
            Player = roster.Player
        });

        await _db.TransferOffers.AddAsync(offer, ct);
        await ExecuteTransferAsync(offer, ct);
        await CancelConflictingOffersAsync(offer, ct);

        AvisosDoTime.Criar(_db, seller.TeamId, AvisosDoTime.VendaPelaLista,
            $"O {buyer.TeamName} comprou {roster.Player.Name} da sua lista de transferências por {price.ToString("C0", BrCulture)}",
            $"/teams/details/{seller.TeamId}", now);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Outro time comprou no mesmo instante: o vínculo com o vendedor já tinha sumido.
            throw new InvalidOperationException("Este jogador acabou de ser negociado com outro time.");
        }

        return MapToDto(offer, buyer, seller, new[] { roster.Player }, Array.Empty<Player>());
    }

    // ── Cláusula de recompra ───────────────────────────────────────────────────

    /// <summary>A venda aceita tinha recompra: quem cedeu o jogador ganha o direito de comprá-lo de volta.</summary>
    private async Task CriarRecompraAsync(TransferOffer offer, decimal preco, DateTime now, CancellationToken ct)
    {
        var vendido = offer.Players.Single();
        var (temporada, _) = await ComissaoService.TemporadaAtualAsync(_db, HorarioDeBrasilia.Agora(_timeProvider), ct);
        _db.DireitosDeRecompra.Add(new DireitoDeRecompra
        {
            DireitoId = Guid.NewGuid(),
            PlayerId = vendido.PlayerId,
            VendedorId = vendido.IsTarget ? offer.ToTeamId : offer.FromTeamId,
            CompradorId = vendido.IsTarget ? offer.FromTeamId : offer.ToTeamId,
            Preco = preco,
            Temporada = temporada,
            OfferId = offer.OfferId,
            CriadoEm = now
        });
    }

    public async Task<IReadOnlyList<DireitoDeRecompraDto>> RecomprasDoTimeAsync(Guid teamId, CancellationToken ct) =>
        (await RecomprasAsync(d => d.VendedorId == teamId || d.CompradorId == teamId, ct))
        .OrderBy(d => !d.Vale).ThenBy(d => d.PlayerName)
        .ToList();

    public async Task<DireitoDeRecompraDto?> RecompraDoJogadorAsync(int playerId, CancellationToken ct) =>
        (await RecomprasAsync(d => d.PlayerId == playerId && d.ExercidaEm == null, ct)).FirstOrDefault(d => d.Vale);

    private async Task<List<DireitoDeRecompraDto>> RecomprasAsync(
        System.Linq.Expressions.Expression<Func<DireitoDeRecompra, bool>> filtro, CancellationToken ct)
    {
        var (temporada, _) = await ComissaoService.TemporadaAtualAsync(_db, HorarioDeBrasilia.Agora(_timeProvider), ct);
        var direitos = await _db.DireitosDeRecompra.AsNoTracking()
            .Where(filtro)
            .Select(d => new
            {
                d.DireitoId, d.PlayerId, d.Player.PlayerGuid, d.Player.Name, d.VendedorId, Vendedor = d.Vendedor.TeamName,
                d.CompradorId, Comprador = d.Comprador.TeamName, d.Preco, d.Temporada, d.ExercidaEm,
                TimeAtual = _db.TeamRosters.Where(r => r.PlayerId == d.PlayerId).Select(r => (Guid?)r.TeamId).FirstOrDefault()
            })
            .ToListAsync(ct);

        return direitos.Select(d =>
        {
            var situacao = d.ExercidaEm is not null ? "recompra feita"
                : d.Temporada != temporada ? "a temporada acabou"
                : d.TimeAtual != d.CompradorId ? $"o jogador saiu do {d.Comprador}"
                : null;
            return new DireitoDeRecompraDto(d.DireitoId, d.PlayerId, d.PlayerGuid, d.Name, d.VendedorId, d.Vendedor,
                d.CompradorId, d.Comprador, d.Preco, situacao is null, situacao);
        }).ToList();
    }

    public Task<TransferOfferListItemDto> RecomprarAsync(Guid teamId, Guid direitoId, CancellationToken ct) =>
        EmTransacaoSerializavelAsync(() => RecomprarNaTransacaoAsync(teamId, direitoId, ct), ct);

    private async Task<TransferOfferListItemDto> RecomprarNaTransacaoAsync(Guid teamId, Guid direitoId, CancellationToken ct)
    {
        var direito = await _db.DireitosDeRecompra.FirstOrDefaultAsync(d => d.DireitoId == direitoId, ct)
            ?? throw new KeyNotFoundException("Cláusula de recompra não encontrada.");
        if (direito.VendedorId != teamId)
            throw new InvalidOperationException("Só quem vendeu o jogador pode recomprar.");
        var situacao = (await RecomprasAsync(d => d.DireitoId == direitoId, ct)).Single();
        if (!situacao.Vale)
            throw new InvalidOperationException($"A recompra não vale mais: {situacao.Situacao}.");

        var roster = await _db.TeamRosters
            .Include(r => r.Player).ThenInclude(p => p.Position)
            .FirstAsync(r => r.PlayerId == direito.PlayerId && r.TeamId == direito.CompradorId, ct);
        var vendedor = await _db.Teams.FirstAsync(t => t.TeamId == direito.VendedorId, ct);
        var comprador = await _db.Teams.FirstAsync(t => t.TeamId == direito.CompradorId, ct);

        var cfg = await _db.TransferConfigs.AsNoTracking().FirstOrDefaultAsync(ct) ?? TransferConfig.Default();
        EnsureMercadoAberto(cfg);
        EnsureTransferLimit(cfg, vendedor);
        await EnsureNotOnLoanAsync([roster.Player], ct);

        // Uma venda já aceita: quem vendeu paga o preço combinado e o jogador volta.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var offer = new TransferOffer
        {
            OfferId = Guid.NewGuid(),
            FromTeamId = vendedor.TeamId,
            ToTeamId = comprador.TeamId,
            Type = OfferType.Sale,
            Status = OfferStatus.Accepted,
            Money = direito.Preco,
            MoneyPayerTeamId = vendedor.TeamId,
            Notes = "Recompra pela cláusula da venda.",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        offer.Players.Add(new TransferOfferPlayer { OfferId = offer.OfferId, PlayerId = roster.PlayerId, IsTarget = true, Player = roster.Player });

        await _db.TransferOffers.AddAsync(offer, ct);
        await ExecuteTransferAsync(offer, ct);
        await CancelConflictingOffersAsync(offer, ct);
        direito.ExercidaEm = now;

        AvisosDoTime.Criar(_db, comprador.TeamId, AvisosDoTime.PropostaAceita,
            $"O {vendedor.TeamName} usou a cláusula de recompra: {roster.Player.Name} volta para ele por {direito.Preco.ToString("C0", BrCulture)}.",
            $"/teams/details/{comprador.TeamId}", now);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("Este jogador acabou de ser negociado com outro time.");
        }

        return MapToDto(offer, vendedor, comprador, new[] { roster.Player }, Array.Empty<Player>());
    }

    private async Task CancelConflictingOffersAsync(TransferOffer acceptedOffer, CancellationToken ct)
    {
        var involvedPlayerIds = acceptedOffer.Players.Select(p => p.PlayerId).ToList();
        await PropostasPendentes.CancelarComJogadoresAsync(_db, involvedPlayerIds, acceptedOffer.OfferId,
            "um jogador dela acabou de ser negociado.", _timeProvider.GetUtcNow().UtcDateTime, ct);

        // Pick que mudou de dono: as outras propostas pendentes com ela não valem mais.
        if (acceptedOffer.Picks.Count == 0) return;
        var chaves = acceptedOffer.Picks.Select(p => $"{p.Temporada}:{p.Rodada}:{p.TimeOriginalId}").ToHashSet();
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var pendentes = await _db.TransferOffers
            .Include(o => o.Picks)
            .Where(o => o.OfferId != acceptedOffer.OfferId && o.Status == OfferStatus.Pending && o.Picks.Any())
            .ToListAsync(ct);
        foreach (var o in pendentes.Where(o => o.Picks.Any(p => chaves.Contains($"{p.Temporada}:{p.Rodada}:{p.TimeOriginalId}"))))
        {
            o.Status = OfferStatus.Cancelled;
            o.UpdatedAtUtc = agora;
            AvisosDoTime.Criar(_db, o.FromTeamId, AvisosDoTime.PropostaCancelada,
                "Sua proposta foi cancelada: uma pick do draft dela acabou de ser negociada.", "/minha-area", agora);
        }
    }

    private static PickRefDto Ref(TransferOfferPick p) => new(p.Temporada, p.Rodada, p.TimeOriginalId);

    /// <summary>As picks da proposta ainda são de quem as cede (e o draft ainda não foi sorteado).</summary>
    private async Task EnsurePicksAsync(TransferOffer offer, CancellationToken ct)
    {
        var agora = HorarioDeBrasilia.Agora(_timeProvider);
        var protecoes = offer.Picks.Where(p => p.ProtegidaAte is not null)
            .ToDictionary(Ref, p => new PickProtecaoDto(Ref(p), p.ProtegidaAte!.Value, p.Compensacao ?? 0m));
        var erro = await PicksDoDraftService.ProblemaAsync(_db, agora, offer.Picks.Where(p => p.IsTarget).Select(Ref),
                       offer.ToTeamId, offer.ToTeam.TeamName, ct, protecoes)
                   ?? await PicksDoDraftService.ProblemaAsync(_db, agora, offer.Picks.Where(p => !p.IsTarget).Select(Ref),
                       offer.FromTeamId, offer.FromTeam.TeamName, ct, protecoes);
        if (erro is not null)
            throw new InvalidOperationException($"A proposta não vale mais: {erro} Recuse-a ou peça uma nova.");
    }

    /// <summary>Negócio só de picks: move o dinheiro (se houver); não conta no limite de transferências.</summary>
    private async Task ExecutarSoPicksAsync(TransferOffer offer, CancellationToken ct)
    {
        if (offer.Money <= 0 || offer.MoneyPayerTeamId is not Guid pagadorId) return;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var recebedorId = pagadorId == offer.FromTeamId ? offer.ToTeamId : offer.FromTeamId;
        var pagador = await _db.Teams.FirstAsync(t => t.TeamId == pagadorId, ct);
        var recebedor = await _db.Teams.FirstAsync(t => t.TeamId == recebedorId, ct);
        if (pagador.Budget - pagador.BudgetBlocked < offer.Money)
            throw new InvalidOperationException($"Saldo insuficiente no time {pagador.TeamName}.");

        pagador.Budget = decimal.Round(pagador.Budget - offer.Money, 2, MidpointRounding.AwayFromZero);
        recebedor.Budget = decimal.Round(recebedor.Budget + offer.Money, 2, MidpointRounding.AwayFromZero);
        var descricao = $"Picks do draft entre {offer.FromTeam.TeamName} e {offer.ToTeam.TeamName}: "
                        + string.Join(", ", offer.Picks.Select(p => $"{p.Rodada}ª rodada do {p.TimeOriginal.TeamName}"));
        ExtratoCaixa.Lancar(_db, pagadorId, -offer.Money, ExtratoCaixa.Transferencia, descricao, now);
        ExtratoCaixa.Lancar(_db, recebedorId, offer.Money, ExtratoCaixa.Transferencia, descricao, now);
    }

    private async Task<Dictionary<Guid, string>> NomesDosTimesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        return lista.Count == 0
            ? []
            : await _db.Teams.AsNoTracking().Where(t => lista.Contains(t.TeamId)).ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);
    }

    private static string NomeDoTime(TransferOfferPick p, IReadOnlyDictionary<Guid, string>? nomes) =>
        p.TimeOriginal?.TeamName ?? nomes?.GetValueOrDefault(p.TimeOriginalId) ?? "?";

    /// <summary>Resumo da proposta do ponto de vista de quem a fez ("pede X, oferece Y, paga R$ 10 mi").</summary>
    private static string Resumo(TransferOffer offer, IEnumerable<Player> alvos, IEnumerable<Player> oferecidos, Guid quemFez,
        IReadOnlyDictionary<Guid, string>? nomes = null)
    {
        var partes = new List<string>();
        string Picks(bool pedidas) => string.Join(", ", offer.Picks.Where(p => p.IsTarget == pedidas)
            .Select(p => $"pick da {p.Rodada}ª rodada do {NomeDoTime(p, nomes)}"
                         + (p.ProtegidaAte is int ate ? $" (protegida até a #{ate})" : "")));
        var pedidos = string.Join(", ", alvos.Select(p => p.Name).Append(Picks(true)).Where(x => x.Length > 0));
        var dados = string.Join(", ", oferecidos.Select(p => p.Name).Append(Picks(false)).Where(x => x.Length > 0));
        if (pedidos.Length > 0) partes.Add((offer.Type == OfferType.Loan ? "empréstimo de " : "pede ") + pedidos);
        if (dados.Length > 0) partes.Add((offer.Type == OfferType.Loan ? "empresta " : "oferece ") + dados);
        if (offer.Money > 0)
            partes.Add($"{(offer.MoneyPayerTeamId == quemFez ? "paga" : "pede")} {offer.Money.ToString("C0", BrCulture)}");
        if (offer.BuybackPrice is decimal recompra)
            partes.Add($"com recompra por {recompra.ToString("C0", BrCulture)}");
        return partes.Count == 0 ? "sem jogadores, picks nem dinheiro" : string.Join(", ", partes);
    }

    private IQueryable<TransferOffer> QueryOffers()
    {
        return _db.TransferOffers
            .Include(o => o.FromTeam)
            .Include(o => o.ToTeam)
            .Include(o => o.Players).ThenInclude(p => p.Player).ThenInclude(p => p.Position)
            .Include(o => o.Picks).ThenInclude(p => p.TimeOriginal)
            .AsNoTracking();
    }

    private async Task ExecuteTransferAsync(TransferOffer offer, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var targetPlayers = offer.Players.Where(p => p.IsTarget).Select(p => p.Player).ToList();
        var offeredPlayers = offer.Players.Where(p => !p.IsTarget).Select(p => p.Player).ToList();
        var allPlayers = targetPlayers.Concat(offeredPlayers).ToList();

        var fromTeam = await _db.Teams.FirstOrDefaultAsync(t => t.TeamId == offer.FromTeamId, ct)
            ?? throw new InvalidOperationException("Time de origem não encontrado.");
        var toTeam = await _db.Teams.FirstOrDefaultAsync(t => t.TeamId == offer.ToTeamId, ct)
            ?? throw new InvalidOperationException("Time de destino não encontrado.");

        var cfg = await _db.TransferConfigs.AsNoTracking().FirstOrDefaultAsync(ct) ?? TransferConfig.Default();
        var pediram = await PediramParaSairAsync(allPlayers, ct);

        // Valida o tamanho do elenco para os times ficarem entre o mínimo e o máximo de jogadores.
        // targetPlayers saem de toTeam e entram em fromTeam; offeredPlayers saem de fromTeam e entram em toTeam.
        await EnsureElencoAsync(cfg, toTeam, targetPlayers.Count, offeredPlayers.Count, ct,
            targetPlayers.Count(p => pediram.ContainsKey(p.PlayerId)));
        await EnsureElencoAsync(cfg, fromTeam, offeredPlayers.Count, targetPlayers.Count, ct,
            offeredPlayers.Count(p => pediram.ContainsKey(p.PlayerId)));

        if (offer.Money > 0 && offer.MoneyPayerTeamId.HasValue)
        {
            var payerTeam = offer.MoneyPayerTeamId.Value == fromTeam.TeamId ? fromTeam : toTeam;
            var receiverTeam = offer.MoneyPayerTeamId.Value == fromTeam.TeamId ? toTeam : fromTeam;

            var available = decimal.Round(payerTeam.Budget - payerTeam.BudgetBlocked, 2, MidpointRounding.AwayFromZero);
            if (available < offer.Money)
                throw new InvalidOperationException($"Saldo insuficiente no time {payerTeam.TeamName}.");

            payerTeam.Budget = decimal.Round(payerTeam.Budget - offer.Money, 2, MidpointRounding.AwayFromZero);
            receiverTeam.Budget = decimal.Round(receiverTeam.Budget + offer.Money, 2, MidpointRounding.AwayFromZero);
        }

        var isLoan = offer.Type == OfferType.Loan;
        var amountCarrierPlayerId = allPlayers.Count > 0
            ? allPlayers.OrderByDescending(p => p.Overall).ThenBy(p => p.PlayerId).First().PlayerId
            : (int?)null;

        var noteParts = new List<string>();
        if (offer.Type == OfferType.Swap)
        {
            noteParts.Add($"Troca entre {fromTeam.TeamName} e {toTeam.TeamName}");
            if (targetPlayers.Count > 0)
                noteParts.Add($"{toTeam.TeamName} envia: {FormatPlayerList(targetPlayers)}");
            if (offeredPlayers.Count > 0)
                noteParts.Add($"{fromTeam.TeamName} envia: {FormatPlayerList(offeredPlayers)}");
        }
        else
        {
            var typeLabel = isLoan ? "Empréstimo" : "Venda";
            if (targetPlayers.Count > 0)
            {
                noteParts.Add($"{typeLabel} de {FormatPlayerList(targetPlayers)}");
                noteParts.Add($"De {toTeam.TeamName} para {fromTeam.TeamName}");
            }
            if (offeredPlayers.Count > 0)
            {
                noteParts.Add($"{typeLabel} de {FormatPlayerList(offeredPlayers)}");
                noteParts.Add($"De {fromTeam.TeamName} para {toTeam.TeamName}");
            }
        }

        if (offer.Money > 0 && offer.MoneyPayerTeamId.HasValue)
        {
            var payerName = offer.MoneyPayerTeamId.Value == fromTeam.TeamId ? fromTeam.TeamName : toTeam.TeamName;
            noteParts.Add($"{payerName} paga {offer.Money.ToString("C", BrCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(offer.Clauses))
            noteParts.Add($"Cláusulas: {offer.Clauses}");

        if (offer.SellOnPercentage > 0)
            noteParts.Add($"Revenda futura: {offer.SellOnPercentage:N2}%");

        if (isLoan)
            noteParts.Add(offer.BuyOptionPrice is decimal opcao
                ? $"Até o fim da temporada, com opção de compra por {opcao.ToString("C", BrCulture)}"
                : "Até o fim da temporada");

        var historyNotes = string.Join(". ", noteParts);
        if (historyNotes.Length > 400)
            historyNotes = historyNotes[..397] + "...";

        if (offer.Money > 0 && offer.MoneyPayerTeamId.HasValue)
        {
            var payerId = offer.MoneyPayerTeamId.Value;
            var receiverId = payerId == fromTeam.TeamId ? toTeam.TeamId : fromTeam.TeamId;
            var origem = offer.Type switch
            {
                OfferType.Swap => ExtratoCaixa.Troca,
                OfferType.Loan => ExtratoCaixa.Emprestimo,
                _ => ExtratoCaixa.Transferencia
            };
            ExtratoCaixa.Lancar(_db, payerId, -offer.Money, origem, historyNotes, now);
            ExtratoCaixa.Lancar(_db, receiverId, offer.Money, origem, historyNotes, now);
        }

        foreach (var player in targetPlayers)
        {
            var tracked = await _db.Players.FirstAsync(p => p.PlayerId == player.PlayerId, ct);
            tracked.CurrentTeamId = offer.FromTeamId;

            var oldRosters = await _db.TeamRosters
                .Where(r => r.PlayerId == player.PlayerId && r.TeamId != offer.FromTeamId)
                .ToListAsync(ct);
            _db.TeamRosters.RemoveRange(oldRosters);

            var exists = await _db.TeamRosters.AnyAsync(r => r.PlayerId == player.PlayerId && r.TeamId == offer.FromTeamId, ct);
            if (!exists)
                await _db.TeamRosters.AddAsync(new TeamRoster { PlayerId = player.PlayerId, TeamId = offer.FromTeamId }, ct);

            await _db.TransferHistories.AddAsync(new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                Type = offer.Type switch
                {
                    OfferType.Swap => TransferType.TeamTrade,
                    OfferType.Loan => TransferType.Loan,
                    _ => TransferType.TeamSale
                },
                PlayerId = player.PlayerId,
                FromTeamId = offer.ToTeamId,
                ToTeamId = offer.FromTeamId,
                Amount = amountCarrierPlayerId == player.PlayerId ? offer.Money : 0m,
                Notes = historyNotes,
                PerformedBy = "system",
                PerformedAtUtc = now,
                OldOverall = player.Overall,
                NewOverall = player.Overall,
                VariacaoEmBaixa = isLoan ? null : pediram.GetValueOrDefault(player.PlayerId)?.Variacao
            }, ct);
        }

        foreach (var player in offeredPlayers)
        {
            var tracked = await _db.Players.FirstAsync(p => p.PlayerId == player.PlayerId, ct);
            tracked.CurrentTeamId = offer.ToTeamId;

            var oldRosters = await _db.TeamRosters
                .Where(r => r.PlayerId == player.PlayerId && r.TeamId != offer.ToTeamId)
                .ToListAsync(ct);
            _db.TeamRosters.RemoveRange(oldRosters);

            var exists = await _db.TeamRosters.AnyAsync(r => r.PlayerId == player.PlayerId && r.TeamId == offer.ToTeamId, ct);
            if (!exists)
                await _db.TeamRosters.AddAsync(new TeamRoster { PlayerId = player.PlayerId, TeamId = offer.ToTeamId }, ct);

            await _db.TransferHistories.AddAsync(new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                Type = isLoan ? TransferType.Loan : TransferType.TeamTrade,
                PlayerId = player.PlayerId,
                FromTeamId = offer.FromTeamId,
                ToTeamId = offer.ToTeamId,
                Amount = amountCarrierPlayerId == player.PlayerId ? offer.Money : 0m,
                Notes = historyNotes,
                PerformedBy = "system",
                PerformedAtUtc = now,
                OldOverall = player.Overall,
                NewOverall = player.Overall,
                VariacaoEmBaixa = isLoan ? null : pediram.GetValueOrDefault(player.PlayerId)?.Variacao
            }, ct);
        }

        if (isLoan)
        {
            await RegisterLoansAsync(offer, fromTeam, toTeam, targetPlayers, offeredPlayers, cfg, now, ct);
            return;
        }

        // Percentual de revenda: quem vendeu paga as cláusulas antigas sobre o que recebeu por jogador
        // (o dinheiro da proposta dividido pelos jogadores que ele cedeu), e nascem as cláusulas desta venda.
        var comoSaiu = offer.Type == OfferType.Swap ? "troca" : "venda";
        var recebidoPeloDestino = offer.MoneyPayerTeamId == fromTeam.TeamId ? offer.Money : 0m;
        var recebidoPelaOrigem = offer.MoneyPayerTeamId == toTeam.TeamId ? offer.Money : 0m;
        foreach (var player in targetPlayers)
        {
            await ClausulasDeRevenda.PagarAsync(_db, player.PlayerId, player.Name, toTeam,
                recebidoPeloDestino / targetPlayers.Count, comoSaiu, now, ct);
            ClausulasDeRevenda.Criar(_db, player.PlayerId, toTeam.TeamId, fromTeam.TeamId, offer.SellOnPercentage, offer.OfferId, now);
        }
        foreach (var player in offeredPlayers)
        {
            await ClausulasDeRevenda.PagarAsync(_db, player.PlayerId, player.Name, fromTeam,
                recebidoPelaOrigem / offeredPlayers.Count, comoSaiu, now, ct);
            ClausulasDeRevenda.Criar(_db, player.PlayerId, fromTeam.TeamId, toTeam.TeamId, offer.SellOnPercentage, offer.OfferId, now);
        }

        fromTeam.TransferCount++;
        if (offer.Type == OfferType.Swap)
            toTeam.TransferCount++;

        var teamsAtLimit = new List<Guid>();
        if (fromTeam.TransferCount >= cfg.MaxTransfersFor(fromTeam)) teamsAtLimit.Add(fromTeam.TeamId);
        if (offer.Type == OfferType.Swap && toTeam.TransferCount >= cfg.MaxTransfersFor(toTeam)) teamsAtLimit.Add(toTeam.TeamId);

        if (teamsAtLimit.Count > 0)
        {
            // Empréstimo tem limite próprio: bater o de transferências não derruba as propostas de empréstimo.
            var now2 = _timeProvider.GetUtcNow().UtcDateTime;
            var offersToCancel = await _db.TransferOffers
                .Where(o => o.OfferId != offer.OfferId
                    && o.Status == OfferStatus.Pending
                    && o.Type != OfferType.Loan
                    && (teamsAtLimit.Contains(o.FromTeamId) || teamsAtLimit.Contains(o.ToTeamId)))
                .ToListAsync(ct);

            foreach (var o in offersToCancel)
            {
                o.Status = OfferStatus.Cancelled;
                o.UpdatedAtUtc = now2;
            }
        }
    }

    /// <summary>
    /// Registra o empréstimo de cada jogador que mudou de time e conta no limite de quem recebeu.
    /// Chegando no limite, as outras propostas de empréstimo em que ele receberia jogador caem.
    /// </summary>
    private async Task RegisterLoansAsync(
        TransferOffer offer, Team fromTeam, Team toTeam,
        IReadOnlyCollection<Player> targetPlayers, IReadOnlyCollection<Player> offeredPlayers,
        TransferConfig cfg, DateTime now, CancellationToken ct)
    {
        var (owner, borrower, players) = targetPlayers.Count > 0
            ? (toTeam, fromTeam, targetPlayers)
            : (fromTeam, toTeam, offeredPlayers);

        foreach (var player in players)
        {
            await _db.Emprestimos.AddAsync(new Emprestimo
            {
                EmprestimoId = Guid.NewGuid(),
                PlayerId = player.PlayerId,
                DonoTeamId = owner.TeamId,
                TomadorTeamId = borrower.TeamId,
                OfferId = offer.OfferId,
                ValorOpcaoCompra = offer.BuyOptionPrice,
                Status = EmprestimoStatus.Ativo,
                InicioUtc = now
            }, ct);
        }

        borrower.LoanCount++;
        if (borrower.LoanCount < cfg.MaxLoans)
            return;

        var borrowerId = borrower.TeamId;
        var offersToCancel = await _db.TransferOffers
            .Where(o => o.OfferId != offer.OfferId
                && o.Status == OfferStatus.Pending
                && o.Type == OfferType.Loan
                && ((o.FromTeamId == borrowerId && o.Players.Any(p => p.IsTarget))
                    || (o.ToTeamId == borrowerId && o.Players.Any(p => !p.IsTarget))))
            .ToListAsync(ct);

        foreach (var o in offersToCancel)
        {
            o.Status = OfferStatus.Cancelled;
            o.UpdatedAtUtc = now;
        }
    }

    /// <summary>Quem recebe o jogador no empréstimo: quem pediu (alvos) ou o outro time (oferecidos).</summary>
    private static Guid LoanBorrower(TransferOffer offer)
        => offer.Players.Any(p => p.IsTarget) ? offer.FromTeamId : offer.ToTeamId;

    private static void EnsureMercadoAberto(TransferConfig cfg)
    {
        if (cfg.MercadoFechado)
            throw new InvalidOperationException("Mercado fechado! As negociações estão bloqueadas até a próxima janela.");
    }

    private static void EnsureLoanLimit(TransferConfig cfg, Team borrower)
    {
        if (borrower.LoanCount >= cfg.MaxLoans)
            throw new InvalidOperationException($"O time {borrower.TeamName} já atingiu o limite de empréstimos da janela ({borrower.LoanCount}/{cfg.MaxLoans}).");
    }

    /// <summary>Quem pediu para sair, com a variação do valor travada (o preço em baixa que vai junto para o clube novo).</summary>
    private sealed record PedidoNoVinculo(string Nome, decimal? Variacao);

    private async Task<Dictionary<int, PedidoNoVinculo>> PediramParaSairAsync(IEnumerable<Player> players, CancellationToken ct)
    {
        var ids = players.Select(p => p.PlayerId).Distinct().ToList();
        if (ids.Count == 0) return [];

        return (await _db.TeamRosters.AsNoTracking()
                .Where(r => ids.Contains(r.PlayerId) && r.SaidaPedidaEm != null)
                .Select(r => new { r.PlayerId, r.Player.Name, r.VariacaoCongelada })
                .ToListAsync(ct))
            .ToDictionary(r => r.PlayerId, r => new PedidoNoVinculo(r.Name, r.VariacaoCongelada));
    }

    /// <summary>Quem pediu para sair só sai vendido ou trocado: emprestar seria escapar da lista e voltar no fim da temporada.</summary>
    private static void EnsureNaoEmprestaQuemPediuParaSair(Dictionary<int, PedidoNoVinculo> pediram)
    {
        if (pediram.Count > 0)
            throw new InvalidOperationException(
                $"{string.Join(", ", pediram.Values.Select(p => p.Nome))} pediu para sair: só sai vendido ou trocado, não emprestado.");
    }

    /// <summary>Jogador emprestado fica parado até voltar: não entra em venda, troca nem outro empréstimo.</summary>
    private async Task EnsureNotOnLoanAsync(IEnumerable<Player> players, CancellationToken ct)
    {
        var ids = players.Select(p => p.PlayerId).Distinct().ToList();
        if (ids.Count == 0) return;

        var emprestado = await _db.Emprestimos.AsNoTracking()
            .Where(e => e.Status == EmprestimoStatus.Ativo && ids.Contains(e.PlayerId))
            .Select(e => e.Player.Name)
            .FirstOrDefaultAsync(ct);

        if (emprestado is not null)
            throw new InvalidOperationException($"{emprestado} está emprestado e não pode ser negociado até o empréstimo acabar.");
    }

    private static void EnsureTransferLimit(TransferConfig cfg, Team team)
    {
        var limite = cfg.MaxTransfersFor(team);
        if (team.TransferCount >= limite)
            throw new InvalidOperationException($"O time {team.TeamName} já atingiu o limite de transferências da janela ({team.TransferCount}/{limite}).");
    }

    private static string FormatPlayerList(IReadOnlyCollection<Player> players)
        => players.Count == 0 ? "Sem jogadores" : string.Join(", ", players.Select(p => $"{p.Name} ({p.Overall})"));

    private static TransferOfferListItemDto MapToDto(TransferOffer offer)
    {
        var targetPlayers = offer.Players.Where(p => p.IsTarget).Select(p => p.Player).ToList();
        var offeredPlayers = offer.Players.Where(p => !p.IsTarget).Select(p => p.Player).ToList();
        return MapToDto(offer, offer.FromTeam, offer.ToTeam, targetPlayers, offeredPlayers);
    }

    private static TransferOfferListItemDto MapToDto(
        TransferOffer offer, Team fromTeam, Team toTeam,
        IReadOnlyCollection<Player> targetPlayers, IReadOnlyCollection<Player> offeredPlayers,
        IReadOnlyDictionary<Guid, string>? nomes = null)
    {
        // A pick pedida é de quem recebe a proposta; a oferecida, de quem propõe.
        List<PickDoDraftDto> Picks(bool pedidas) => offer.Picks.Where(p => p.IsTarget == pedidas)
            .Select(p => new PickDoDraftDto(p.Temporada, p.Rodada, p.TimeOriginalId, NomeDoTime(p, nomes),
                pedidas ? toTeam.TeamId : fromTeam.TeamId, pedidas ? toTeam.TeamName : fromTeam.TeamName,
                ProtegidaAte: p.ProtegidaAte, ProtetorNome: p.ProtegidaAte is null ? null : pedidas ? toTeam.TeamName : fromTeam.TeamName,
                Compensacao: p.Compensacao))
            .ToList();

        string? moneyPayerName = null;
        if (offer.MoneyPayerTeamId.HasValue)
        {
            moneyPayerName = offer.MoneyPayerTeamId.Value == fromTeam.TeamId
                ? fromTeam.TeamName
                : toTeam.TeamName;
        }

        return new TransferOfferListItemDto(
            offer.OfferId,
            offer.FromTeamId,
            fromTeam.TeamName,
            offer.ToTeamId,
            toTeam.TeamName,
            offer.Type.ToString(),
            offer.Status.ToString(),
            offer.Money,
            offer.MoneyPayerTeamId,
            moneyPayerName,
            offer.SellOnPercentage,
            offer.Clauses,
            offer.Notes,
            offer.ParentOfferId,
            targetPlayers.Select(MapPlayerDto).ToList(),
            offeredPlayers.Select(MapPlayerDto).ToList(),
            offer.CreatedAtUtc,
            offer.UpdatedAtUtc,
            offer.BuyOptionPrice,
            Picks(true),
            Picks(false),
            offer.BuybackPrice);
    }

    private static TransferOfferPlayerDto MapPlayerDto(Player p)
    {
        return new TransferOfferPlayerDto(
            p.PlayerId,
            p.PlayerGuid,
            p.Name,
            p.Position?.Name ?? "?",
            p.Overall,
            p.Age);
    }

    private static bool PlayerBelongsToTeam(Player player, Guid teamId)
    {
        if (player.CurrentTeamId == teamId)
            return true;

        return player.TeamRosters?.Any(r => r.TeamId == teamId) ?? false;
    }
}
