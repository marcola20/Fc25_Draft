using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Cadastro das pessoas da liga e das passagens delas pelos clubes.</summary>
public interface ITreinadorService
{
    Task<IReadOnlyList<TreinadorDto>> ListAsync(bool incluirInativos, CancellationToken ct);

    Task<TreinadorDto?> GetAsync(Guid treinadorId, CancellationToken ct);

    /// <summary>Quem está com este token, seja treinador ou auxiliar.</summary>
    Task<TreinadorDto?> GetPorTokenAsync(string token, CancellationToken ct);

    /// <summary>Cria ou atualiza o cadastro. Sem token informado, gera um.</summary>
    Task<TreinadorDto> SalvarAsync(TreinadorSalvarRequest request, CancellationToken ct);

    Task ExcluirAsync(Guid treinadorId, CancellationToken ct);

    /// <summary>Sorteia um token novo. O antigo para de funcionar na hora.</summary>
    Task<TreinadorDto> RegerarTokenAsync(Guid treinadorId, CancellationToken ct);

    /// <summary>Registra uma passagem por um clube. Recusa se a pessoa já estiver em outro time no período.</summary>
    Task<TreinadorDto> RegistrarPassagemAsync(TreinadorPassagemRequest request, CancellationToken ct);

    /// <summary>Encerra a passagem atual (saída do clube).</summary>
    Task<TreinadorDto> EncerrarPassagemAsync(Guid passagemId, DateTime ate, CancellationToken ct);

    Task RemoverPassagemAsync(Guid passagemId, CancellationToken ct);

    /// <summary>Carreira completa: clubes, temporadas e títulos.</summary>
    Task<TreinadorCarreiraDto?> GetCarreiraAsync(Guid treinadorId, CancellationToken ct);
}
