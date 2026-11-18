using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fc25Draft.Web.Extensions.DI;

public static class ArtesServiceCollectionExtensions
{
    /// <summary>Artes automáticas (fila para o Artista CBFV).</summary>
    public static IServiceCollection AddArtesFeature(this IServiceCollection services)
    {
        services.AddScoped<IArtesService, ArtesService>();
        return services;
    }
}
