using Fc25Draft.Web.Hubs;
using Fc25Draft.Web.Services;

namespace Fc25Draft.Web.Extensions.DI
{
    public static class WebDI
    {
        public static IServiceCollection AddWebServices(this IServiceCollection services)
        {
            services.AddScoped<AdminAuthService>();
            services.AddScoped<ApiClientFactory>();
            services.AddScoped<PlayersApiClient>();
            services.AddScoped<DraftAdminApiClient>();
            services.AddScoped<TeamsApiClient>();
            services.AddScoped<AdminTransfersApiClient>();
            services.AddScoped<BudgetsApiClient>();
            services.AddScoped<MarketApiClient>();
            services.AddScoped<TeamAccessService>();
            services.AddScoped<MarketClient>();
            services.AddScoped<MarketHubClient>();
            return services;
        }
    }
}
