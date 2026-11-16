using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Guarda e carrega o catálogo da escalação (<see cref="Fc25Draft.Core.Utilities.LineupTemplateCatalog"/>).</summary>
public interface ICatalogoEscalacaoService
{
    /// <summary>Lê o catálogo salvo e passa a usá-lo. Sem nada salvo, segue o de fábrica.</summary>
    Task CarregarAsync(CancellationToken ct);

    /// <summary>O catálogo em uso, com quantas escalações usam cada formação.</summary>
    Task<CatalogoEscalacaoAdminDto> ObterAsync(CancellationToken ct);

    /// <summary>
    /// Valida, grava e passa a usar o catálogo. Formação que alguma escalação usa não pode sumir, mudar de nome nem
    /// trocar os códigos dos lugares (pode ser desativada, e os nomes e as posições dos lugares podem mudar).
    /// Lança <see cref="InvalidOperationException"/> com o motivo.
    /// </summary>
    Task SalvarAsync(CatalogoEscalacao catalogo, CancellationToken ct);
}
