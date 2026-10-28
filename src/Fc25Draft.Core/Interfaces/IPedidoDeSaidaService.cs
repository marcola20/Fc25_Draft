using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>
/// "Pediu para sair" (<see cref="PedidoDeSaida"/>): o insatisfeito que segue no banco vai sozinho para a lista de
/// transferências, e o dono só tira com a promessa de colocar para jogar.
/// </summary>
public interface IPedidoDeSaidaService
{
    /// <summary>
    /// Põe na lista quem pediu para sair e confere as promessas (cumprida ou quebrada). Retorna quantos mudaram.
    /// </summary>
    Task<int> ProcessarAsync(CancellationToken ct);

    /// <summary>Tira da lista quem pediu para sair, com a promessa de colocar para jogar.</summary>
    Task PrometerAsync(Guid teamId, Guid playerGuid, CancellationToken ct);
}
