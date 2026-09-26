using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IEmprestimoService
{
    /// <summary>Empréstimos em andamento; com <paramref name="teamId"/>, só os em que o time é dono ou tomador.</summary>
    Task<IReadOnlyList<EmprestimoDto>> ListarAtivosAsync(Guid? teamId, CancellationToken ct);

    /// <summary>O tomador paga o valor da opção de compra ao dono e fica com o jogador em definitivo.</summary>
    Task<EmprestimoDto> ExercerOpcaoCompraAsync(Guid emprestimoId, string? teamToken, CancellationToken ct);

    /// <summary>Encerra um empréstimo antes da hora (admin): o jogador volta para o dono.</summary>
    Task DevolverAsync(Guid emprestimoId, string performedBy, CancellationToken ct);

    /// <summary>Fim de temporada: todos os emprestados voltam para os donos. Devolve quantos voltaram.</summary>
    Task<int> DevolverTodosAsync(string performedBy, CancellationToken ct);
}
