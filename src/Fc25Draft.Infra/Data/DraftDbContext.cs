using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Data;

public class DraftDbContext : DbContext
{
    public DraftDbContext(DbContextOptions<DraftDbContext> options) : base(options)
    {
        // Avisos novos gravados: as telas abertas desses times atualizam o sino na hora.
        SavedChanges += (_, _) =>
        {
            if (TimesComAvisoNovo.Count == 0) return;
            var times = TimesComAvisoNovo.ToArray();
            TimesComAvisoNovo.Clear();
            AvisosGravados?.Invoke(times);
        };
        SaveChangesFailed += (_, _) => TimesComAvisoNovo.Clear();
    }

    /// <summary>Times que ganharam aviso desde o último SaveChanges deste contexto.</summary>
    internal HashSet<Guid> TimesComAvisoNovo { get; } = new();

    /// <summary>Disparado depois de gravar avisos, com os times que os receberam.</summary>
    public static event Action<IReadOnlyCollection<Guid>>? AvisosGravados;

    // No mesmo save: quem sai do elenco sai das escalações do time (EscalacoesDeQuemSaiu) e a competição
    // encerrada com campeão entra no Hall da Fama (HallDaFamaAutomatico).
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await EscalacoesDeQuemSaiu.TirarAsync(this, cancellationToken);
        await HallDaFamaAutomatico.AtualizarAsync(this, cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EscalacoesDeQuemSaiu.TirarAsync(this, CancellationToken.None).GetAwaiter().GetResult();
        HallDaFamaAutomatico.AtualizarAsync(this, CancellationToken.None).GetAwaiter().GetResult();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<PlayerAtributos> PlayerAtributos => Set<PlayerAtributos>();
    public DbSet<EvolucaoPes> EvolucoesPes => Set<EvolucaoPes>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Draft> Drafts => Set<Draft>();
    public DbSet<DraftRound> DraftRounds => Set<DraftRound>();
    public DbSet<DraftPick> DraftPicks => Set<DraftPick>();
    public DbSet<DraftProtecao> DraftProtecoes => Set<DraftProtecao>();
    public DbSet<DraftAutoPick> DraftAutoPicks => Set<DraftAutoPick>();
    public DbSet<DraftAutoPickRodada> DraftAutoPickRodadas => Set<DraftAutoPickRodada>();
    public DbSet<DraftAutoPickItem> DraftAutoPickItens => Set<DraftAutoPickItem>();
    public DbSet<DraftPlanejadoRodada> DraftPlanejadoRodadas => Set<DraftPlanejadoRodada>();
    public DbSet<DraftAutoPickPrevia> DraftAutoPickPrevias => Set<DraftAutoPickPrevia>();
    public DbSet<DraftAutoPickPreviaRodada> DraftAutoPickPreviaRodadas => Set<DraftAutoPickPreviaRodada>();
    public DbSet<DraftAutoPickPreviaItem> DraftAutoPickPreviaItens => Set<DraftAutoPickPreviaItem>();
    public DbSet<TeamObservacao> TeamObservacoes => Set<TeamObservacao>();
    public DbSet<LigaCopaPote> LigaCopaPotes => Set<LigaCopaPote>();
    public DbSet<Premiacao> Premiacoes => Set<Premiacao>();
    public DbSet<Treinador> Treinadores => Set<Treinador>();
    public DbSet<TreinadorPassagem> TreinadorPassagens => Set<TreinadorPassagem>();
    public DbSet<BolaoPalpite> BolaoPalpites => Set<BolaoPalpite>();
    public DbSet<Regulamento> Regulamentos => Set<Regulamento>();
    public DbSet<AberturaTemporada> AberturasTemporada => Set<AberturaTemporada>();
    public DbSet<PremiacaoItem> PremiacaoItens => Set<PremiacaoItem>();
    public DbSet<PremiacaoPagamento> PremiacaoPagamentos => Set<PremiacaoPagamento>();
    public DbSet<TeamRoster> TeamRosters => Set<TeamRoster>();
    public DbSet<TeamLineup> TeamLineups => Set<TeamLineup>();
    public DbSet<TeamLineupSlot> TeamLineupSlots => Set<TeamLineupSlot>();
    public DbSet<TeamLineupOffensiveInstructions> TeamLineupOffensiveInstructions => Set<TeamLineupOffensiveInstructions>();
    public DbSet<TeamLineupDefensiveInstructions> TeamLineupDefensiveInstructions => Set<TeamLineupDefensiveInstructions>();
    public DbSet<TeamLineupAdvancedInstructions> TeamLineupAdvancedInstructions => Set<TeamLineupAdvancedInstructions>();
    public DbSet<AdminActionsLog> AdminActionsLogs => Set<AdminActionsLog>();
    public DbSet<MarketCycle> MarketCycles => Set<MarketCycle>();
    public DbSet<MarketItem> MarketItems => Set<MarketItem>();
    public DbSet<MarketBid> MarketBids => Set<MarketBid>();
    public DbSet<MarketTransaction> MarketTransactions => Set<MarketTransaction>();
    public DbSet<TransferHistory> TransferHistories => Set<TransferHistory>();
    public DbSet<BudgetLedger> BudgetLedgers => Set<BudgetLedger>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();
    public DbSet<TransferOffer> TransferOffers => Set<TransferOffer>();
    public DbSet<TransferOfferPlayer> TransferOfferPlayers => Set<TransferOfferPlayer>();
    public DbSet<Emprestimo> Emprestimos => Set<Emprestimo>();
    public DbSet<AdminToken> AdminTokens => Set<AdminToken>();

    public DbSet<Liga> Ligas => Set<Liga>();
    public DbSet<LigaRodada> LigaRodadas => Set<LigaRodada>();
    public DbSet<LigaPartida> LigaPartidas => Set<LigaPartida>();
    public DbSet<LigaEventoPartida> LigaEventos => Set<LigaEventoPartida>();
    public DbSet<LigaEscalacaoPartida> LigaEscalacoes => Set<LigaEscalacaoPartida>();
    public DbSet<LigaPartidaImportacao> LigaPartidaImportacoes => Set<LigaPartidaImportacao>();
    public DbSet<LigaNotaJogador> LigaNotasJogadores => Set<LigaNotaJogador>();
    public DbSet<AvisoTime> AvisosTimes => Set<AvisoTime>();
    public DbSet<ClausulaRevenda> ClausulasRevenda => Set<ClausulaRevenda>();
    public DbSet<InscricaoPush> InscricoesPush => Set<InscricaoPush>();
    public DbSet<ChavePush> ChavesPush => Set<ChavePush>();
    public DbSet<NotificacaoEnviada> NotificacoesEnviadas => Set<NotificacaoEnviada>();
    public DbSet<FotoJogador> FotosJogadores => Set<FotoJogador>();
    public DbSet<PartidaChatMensagem> PartidaChatMensagens => Set<PartidaChatMensagem>();
    public DbSet<LigaClassificacao> LigaClassificacoes => Set<LigaClassificacao>();
    public DbSet<LigaPunicao> LigaPunicoes => Set<LigaPunicao>();
    public DbSet<LigaKnockoutJogo> LigaKnockoutJogos => Set<LigaKnockoutJogo>();
    public DbSet<LigaLoteria> LigaLoterias => Set<LigaLoteria>();
    public DbSet<LigaLoteriaPick> LigaLoteriaPicks => Set<LigaLoteriaPick>();
    public DbSet<LigaGrupoTime> LigaGruposTimes => Set<LigaGrupoTime>();
    public DbSet<LigaTime> LigaTimes => Set<LigaTime>();

    public DbSet<PricingConfig> PricingConfigs => Set<PricingConfig>();
    public DbSet<TransferConfig> TransferConfigs => Set<TransferConfig>();

    public DbSet<HallOfFameEntry> HallOfFame => Set<HallOfFameEntry>();

    public DbSet<DraftWishlistEdicao> DraftWishlistEdicoes => Set<DraftWishlistEdicao>();
    public DbSet<DraftWishlistEntry> DraftWishlistEntries => Set<DraftWishlistEntry>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasPostgresExtension("unaccent");
        mb.ApplyConfigurationsFromAssembly(typeof(DraftDbContext).Assembly);
    }
}
