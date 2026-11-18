using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Números das camisas do elenco (1 a 99, sem repetir no time).</summary>
public interface ICamisasService
{
    public const short Menor = 1;
    public const short Maior = 99;

    /// <summary>O elenco com o número de cada um (ordem: com número primeiro, pelo número; depois pelo nome).</summary>
    Task<CamisasDoTimeDto> DoTimeAsync(Guid teamId, CancellationToken ct);

    /// <summary>
    /// Grava os números do time. Só mexe em quem vem em <paramref name="camisas"/>; quem não vem fica como está.
    /// Recusa (InvalidOperationException) número fora de 1–99, jogador que não é do time e número repetido.
    /// </summary>
    Task SalvarAsync(Guid teamId, IReadOnlyList<CamisaRequest> camisas, CancellationToken ct);

    /// <summary>
    /// Camisas que estão no jogo (Editor PES): preenche só quem ainda não tem número no site e só quando o número
    /// está livre no time dele. Nunca troca um número que o técnico já escolheu.
    /// </summary>
    Task<CamisasDoPesResultadoDto> PreencherDoPesAsync(IReadOnlyList<CamisaRequest> itens, CancellationToken ct);
}
