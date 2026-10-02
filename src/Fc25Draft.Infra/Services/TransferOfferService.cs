using System.Globalization;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
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

        if (distinctTargets.Length == 0 && distinctOffered.Length == 0)
            throw new ArgumentException("Selecione ao menos um jogador na negociação.");

        if (dto.Type == OfferType.Swap && distinctOffered.Length == 0)
            throw new ArgumentException("Selecione ao menos um jogador para enviar na troca.");

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

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var fromTeam = await _db.Teams.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TeamId == dto.FromTeamId, ct)
            ?? throw new KeyNotFoundException("Time de origem não encontrado.");

        var toTeam = await _db.Teams.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TeamId == dto.ToTeamId, ct)
            ?? throw new KeyNotFoundException("Time de destino não encontrado.");

        var cfg = await _db.TransferConfigs.AsNoTracking().FirstOrDefaultAsync(ct) ?? TransferConfig.Default();
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

        // Os alvos saem do time de destino e os oferecidos saem do time de origem.
        await EnsureElencoAsync(cfg, toTeam, targetPlayers.Count, offeredPlayers.Count, ct);
        await EnsureElencoAsync(cfg, fromTeam, offeredPlayers.Count, targetPlayers.Count, ct);

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

        AvisosDoTime.Criar(_db, toTeam.TeamId, AvisosDoTime.Proposta,
            $"{(dto.ParentOfferId.HasValue ? "Contraproposta" : "Proposta")} do {fromTeam.TeamName}: {Resumo(offer, targetPlayers, offeredPlayers, fromTeam.TeamId)}",
            "/minha-area", now);

        await _db.TransferOffers.AddAsync(offer, ct);
        await _db.SaveChangesAsync(ct);

        return MapToDto(offer, fromTeam, toTeam, targetPlayers, offeredPlayers);
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
            .Include(o => o.FromTeam)
            .Include(o => o.ToTeam)
            .FirstOrDefaultAsync(o => o.OfferId == offerId, ct)
            ?? throw new KeyNotFoundException("Proposta não encontrada.");

        if (offer.ToTeamId != teamId)
            throw new InvalidOperationException("Apenas o time destinatário pode responder a esta proposta.");

        if (offer.Status != OfferStatus.Pending)
            throw new InvalidOperationException("Esta proposta já foi respondida.");

        if (response == OfferStatus.Accepted)
        {
            var cfg = await _db.TransferConfigs.AsNoTracking().FirstOrDefaultAsync(ct) ?? TransferConfig.Default();
            EnsureMercadoAberto(cfg);
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
            await ExecuteTransferAsync(offer, ct);
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
                r.ListedAtUtc ?? DateTime.MinValue))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Quem cede jogador não pode ficar abaixo do mínimo de elenco (quem já está abaixo só contrata até
    /// voltar a ele), e quem recebe não pode passar do máximo.
    /// </summary>
    private async Task EnsureElencoAsync(TransferConfig cfg, Team team, int saem, int entram, CancellationToken ct)
    {
        if (saem == entram) return;

        var elenco = await _db.TeamRosters.CountAsync(r => r.TeamId == team.TeamId, ct);
        var depois = elenco - saem + entram;

        if (entram > saem && depois > cfg.MaxRosterSize)
            throw new InvalidOperationException(
                $"O {team.TeamName} ficaria com {depois} jogadores; o máximo é {cfg.MaxRosterSize}.");

        var minimo = cfg.MinRosterSizeFor(team);
        if (saem > entram && depois < minimo)
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

    private async Task CancelConflictingOffersAsync(TransferOffer acceptedOffer, CancellationToken ct)
    {
        var involvedPlayerIds = acceptedOffer.Players.Select(p => p.PlayerId).ToList();
        await PropostasPendentes.CancelarComJogadoresAsync(_db, involvedPlayerIds, acceptedOffer.OfferId,
            "um jogador dela acabou de ser negociado.", _timeProvider.GetUtcNow().UtcDateTime, ct);
    }

    /// <summary>Resumo da proposta do ponto de vista de quem a fez ("pede X, oferece Y, paga R$ 10 mi").</summary>
    private static string Resumo(TransferOffer offer, IEnumerable<Player> alvos, IEnumerable<Player> oferecidos, Guid quemFez)
    {
        var partes = new List<string>();
        var pedidos = string.Join(", ", alvos.Select(p => p.Name));
        var dados = string.Join(", ", oferecidos.Select(p => p.Name));
        if (pedidos.Length > 0) partes.Add((offer.Type == OfferType.Loan ? "empréstimo de " : "pede ") + pedidos);
        if (dados.Length > 0) partes.Add((offer.Type == OfferType.Loan ? "empresta " : "oferece ") + dados);
        if (offer.Money > 0)
            partes.Add($"{(offer.MoneyPayerTeamId == quemFez ? "paga" : "pede")} {offer.Money.ToString("C0", BrCulture)}");
        return partes.Count == 0 ? "sem jogadores nem dinheiro" : string.Join(", ", partes);
    }

    private IQueryable<TransferOffer> QueryOffers()
    {
        return _db.TransferOffers
            .Include(o => o.FromTeam)
            .Include(o => o.ToTeam)
            .Include(o => o.Players).ThenInclude(p => p.Player).ThenInclude(p => p.Position)
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

        // Valida o tamanho do elenco para os times ficarem entre o mínimo e o máximo de jogadores.
        // targetPlayers saem de toTeam e entram em fromTeam; offeredPlayers saem de fromTeam e entram em toTeam.
        await EnsureElencoAsync(cfg, toTeam, targetPlayers.Count, offeredPlayers.Count, ct);
        await EnsureElencoAsync(cfg, fromTeam, offeredPlayers.Count, targetPlayers.Count, ct);

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
                NewOverall = player.Overall
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
                NewOverall = player.Overall
            }, ct);
        }

        if (isLoan)
        {
            await RegisterLoansAsync(offer, fromTeam, toTeam, targetPlayers, offeredPlayers, cfg, now, ct);
            return;
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
        IReadOnlyCollection<Player> targetPlayers, IReadOnlyCollection<Player> offeredPlayers)
    {
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
            offer.BuyOptionPrice);
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
