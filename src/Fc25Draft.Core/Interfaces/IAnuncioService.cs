using Fc25Draft.Core.DTOs;

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

    Task ExcluirAsync(Guid anuncioId, CancellationToken ct);
}
