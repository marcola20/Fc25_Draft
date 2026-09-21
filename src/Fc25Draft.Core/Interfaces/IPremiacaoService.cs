using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Premiação por temporada: cadastro (copiando a anterior) e pagamento no caixa dos times.</summary>
public interface IPremiacaoService
{
    Task<IReadOnlyList<PremiacaoDto>> ListAsync(CancellationToken ct);

    Task<PremiacaoDto?> GetAsync(Guid premiacaoId, CancellationToken ct);

    /// <summary>Premiação da temporada; nulo quando ainda não foi cadastrada.</summary>
    Task<PremiacaoDto?> GetPorTemporadaAsync(int temporada, CancellationToken ct);

    /// <summary>Temporada mais recente que já tem premiação cadastrada.</summary>
    Task<PremiacaoDto?> GetAtualAsync(CancellationToken ct);

    Task<PremiacaoDto> CriarAsync(PremiacaoCriarRequest request, CancellationToken ct);

    Task<PremiacaoDto> SalvarItensAsync(Guid premiacaoId, IReadOnlyList<PremiacaoItemInput> itens, CancellationToken ct);

    Task RenomearAsync(Guid premiacaoId, string nome, CancellationToken ct);

    Task ExcluirAsync(Guid premiacaoId, CancellationToken ct);

    /// <summary>Quem recebe o quê em cada competição da temporada.</summary>
    Task<IReadOnlyList<PremiacaoPreviaDto>> ListPreviasAsync(int temporada, CancellationToken ct);

    Task<PremiacaoPreviaDto?> GetPreviaAsync(Guid ligaId, CancellationToken ct);

    /// <summary>Credita a premiação da competição no caixa dos times.</summary>
    Task<PremiacaoPreviaDto> PagarAsync(Guid ligaId, CancellationToken ct);

    /// <summary>Desfaz o pagamento de uma competição, devolvendo os valores.</summary>
    Task EstornarAsync(Guid ligaId, CancellationToken ct);
}
