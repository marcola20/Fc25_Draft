using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>
/// Cadastro dos administradores da liga. Só o administrador principal mexe aqui:
/// toda operação recebe o token de quem está pedindo e confere isso.
/// </summary>
public interface IAdministradorService
{
    /// <summary>Se o token é do administrador principal (e está ativo).</summary>
    Task<bool> EhPrincipalAsync(string? token, CancellationToken ct);

    Task<IReadOnlyList<AdministradorDto>> ListAsync(string tokenDoPrincipal, CancellationToken ct);

    Task<AdministradorDto> SalvarAsync(string tokenDoPrincipal, AdministradorSalvarRequest request, CancellationToken ct);

    /// <summary>Sorteia um token novo. O antigo para de funcionar na hora.</summary>
    Task<AdministradorDto> RegerarTokenAsync(string tokenDoPrincipal, Guid adminTokenId, CancellationToken ct);

    Task ExcluirAsync(string tokenDoPrincipal, Guid adminTokenId, CancellationToken ct);
}
