using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Textos de anúncio das novidades (para o WhatsApp) e a explicação de cada regra, guardados no admin.</summary>
public interface IAnuncioService
{
    /// <summary>Todos os anúncios: grupos mais novos primeiro, e na ordem dentro do grupo.</summary>
    Task<IReadOnlyList<AnuncioDto>> ListAsync(CancellationToken ct);

    Task<AnuncioDto> CriarAsync(AnuncioSalvarRequest request, CancellationToken ct);

    Task<AnuncioDto> SalvarAsync(Guid anuncioId, AnuncioSalvarRequest request, CancellationToken ct);

    /// <summary>Marca (ou desmarca) como mandado no grupo.</summary>
    Task<AnuncioDto> MarcarEnviadoAsync(Guid anuncioId, bool enviado, CancellationToken ct);

    /// <summary>Põe (ou tira) o anúncio do site. Publicar pede resumo e texto do site.</summary>
    Task<AnuncioDto> PublicarAsync(Guid anuncioId, bool publicar, CancellationToken ct);

    Task ExcluirAsync(Guid anuncioId, CancellationToken ct);

    /// <summary>Os publicados no site, do mais novo para o mais antigo.</summary>
    Task<IReadOnlyList<AnuncioPublicadoDto>> ListPublicadosAsync(CancellationToken ct);

    Task<AnuncioPublicadoDto?> GetPublicadoAsync(Guid anuncioId, CancellationToken ct);

    /// <summary>Cada anúncio publicado vira uma notícia no Plantão.</summary>
    Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct);
}
