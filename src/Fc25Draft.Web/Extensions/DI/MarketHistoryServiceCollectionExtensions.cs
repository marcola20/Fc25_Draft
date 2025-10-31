using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fc25Draft.Web.Extensions.DI;

public static class MarketHistoryServiceCollectionExtensions
{
    public static IServiceCollection AddMarketHistoryFeature(this IServiceCollection services)
    {
        services.AddScoped<IMarketHistoryQueryService, MarketHistoryQueryService>();
        return services;
    }
}
