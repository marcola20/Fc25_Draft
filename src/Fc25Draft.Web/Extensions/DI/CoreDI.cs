using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Options;
using Fc25Draft.Infra.Repositories;
using Fc25Draft.Infra.Services;
using Fc25Draft.Web.Hubs;
using Fc25Draft.Web.Options;
using Fc25Draft.Web.Security;
using Fc25Draft.Web.Services;

namespace Fc25Draft.Web.Extensions.DI
{
    public static class CoreDI
    {
        public static IServiceCollection AddCoreServices(this IServiceCollection services, IConfiguration cfg)
        {
            // Options
            services.Configure<AppOptions>(cfg.GetSection(AppOptions.SectionName));
            services.Configure<SecurityOptions>(cfg.GetSection(SecurityOptions.SectionName));
            services.Configure<MarketOptions>(cfg.GetSection(MarketOptions.SectionName));
            services.Configure<EconomiaOptions>(cfg.GetSection(EconomiaOptions.SectionName));

            // Core/Infra
            // AddComConexaoPropria: serviços usados direto pelas páginas, cada um com a sua conexão
            // (ver InfraDI). Os que dividem transação com outro serviço continuam AddScoped.
            services.AddScoped<DraftService>();
            services.AddScoped<DraftStateService>();
            services.AddScoped<DraftExpansaoService>();
            services.AddScoped<DraftAutoPickService>();
            services.AddScoped<DraftAdminService>();
            services.AddComConexaoPropria<ITeamService, TeamService>();
            services.AddScoped<IPlayerService, PlayerService>();
            services.AddComConexaoPropria<IPositionService, PositionService>();
            services.AddScoped<IPricingService, PricingService>();
            services.AddComConexaoPropria<IPricingConfigService, PricingConfigService>();
            services.AddComConexaoPropria<ITransferConfigService, TransferConfigService>();
            services.AddScoped<IMarketCycleGenerator, MarketCycleGenerator>();
            services.AddScoped<IMarketCycleAdminService, MarketCycleAdminService>();
            services.AddScoped<IMarketCycleService, MarketCycleService>();
            services.AddScoped<IMarketItemGenerationService, MarketItemGenerationService>();
            services.AddScoped<IMarketService, MarketService>();
            services.AddScoped<IAuctionSettlementService, AuctionSettlementService>();
            services.AddScoped<ITransactionLogService, TransactionLogService>();
            services.AddScoped<IMarketItemPublicationService, MarketItemPublicationService>();
            services.AddScoped<IMarketItemsQueryService, MarketItemsQueryService>();
            services.AddScoped<IBudgetService, BudgetService>();
            services.AddScoped<AdminTransferService>();
            services.AddScoped<ITransfersQueryService, TransfersQueryService>();
            services.AddScoped<ITransferHistoryService, TransferHistoryService>();
            services.AddScoped<ITeamQuickSellService, TeamQuickSellService>();
            services.AddScoped<ITeamLineupService, TeamLineupService>();
            services.AddScoped<ITransferOfferService, TransferOfferService>();
            services.AddScoped<ILigaAdminService, LigaAdminService>();
            services.AddComConexaoPropria<ILigaPublicService, LigaPublicService>();
            services.AddComConexaoPropria<ITermometroMercadoService, TermometroMercadoService>();
            services.AddComConexaoPropria<IMinhaAreaService, MinhaAreaService>();
            services.AddScoped<ILigaTemporadaService, LigaTemporadaService>();
            services.AddComConexaoPropria<IPremiacaoService, PremiacaoService>();
            services.AddComConexaoPropria<IRegulamentoService, RegulamentoService>();
            services.AddComConexaoPropria<IHallOfFameService, HallOfFameService>();
            services.AddScoped<IDraftWishlistService, DraftWishlistService>();
            services.AddSingleton<LotteryStateService>();
            services.AddSingleton<CopaSorteioAoVivoService>();
            services.AddSingleton<MarketUpdateService>();
            services.AddSingleton<IMarketBroadcaster>(sp => sp.GetRequiredService<MarketUpdateService>());

            // Web layer
            services.AddScoped<AdminAuthService>();
            services.AddScoped<ApiClientFactory>();
            services.AddScoped<PlayersApiClient>();
            services.AddScoped<DraftAdminApiClient>();
            services.AddScoped<TeamsApiClient>();
            services.AddScoped<LineupsApiClient>();
            services.AddScoped<AdminLineupsApiClient>();
            services.AddScoped<AdminTransfersApiClient>();
            services.AddScoped<BudgetsApiClient>();
            services.AddScoped<MarketApiClient>();
            services.AddScoped<MarketClient>();
            services.AddScoped<TransfersClient>();
            services.AddScoped<TransferOffersApiClient>();
            services.AddScoped<DraftWishlistApiClient>();
            services.AddScoped<MarketCycleClient>();
            services.AddScoped<IMarketItemGenerationClient, MarketItemGenerationClient>();
            services.AddScoped<MarketHubClient>();
            services.AddScoped<TeamAccessService>();
            services.AddScoped<ToastService>();
            services.AddSingleton<EscudoService>();
            services.AddScoped<LayoutNavigationService>();
            services.AddScoped<LayoutState>();

            services.AddHttpClient();
            services.AddSignalR();

            return services;
        }
    }
}
