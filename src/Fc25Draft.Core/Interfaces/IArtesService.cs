using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>
/// Artes automáticas: o site acha o que merece arte (vencedores dos prêmios publicados, contratações que passam
/// do valor ou do over mínimo) e põe na fila; o Artista CBFV, no PC do admin, busca pela API com o token de admin,
/// faz o vídeo e a imagem, manda no Telegram e avisa que terminou.
/// </summary>
public interface IArtesService
{
    /// <summary>Procura fatos novos (se estiver ligado), põe na fila e devolve os pendentes com os dados prontos.</summary>
    Task<IReadOnlyList<PedidoArteDto>> PendentesAsync(CancellationToken ct);

    /// <summary>O agente terminou (ok) ou desistiu (erro) de um pedido.</summary>
    Task ConcluirAsync(Guid pedidoId, bool ok, string? erro, CancellationToken ct);

    /// <summary>Só procura fatos novos e põe na fila; devolve quantos entraram.</summary>
    Task<int> ProcurarAsync(CancellationToken ct);

    Task<IReadOnlyList<PedidoArteResumoDto>> ListarAsync(int quantos, CancellationToken ct);
    Task RefazerAsync(Guid pedidoId, CancellationToken ct);
    Task DescartarAsync(Guid pedidoId, CancellationToken ct);

    Task<ArtesConfigDto> ConfigAsync(CancellationToken ct);
    Task SalvarConfigAsync(bool ligado, decimal valorMinimo, int overMinimo, CancellationToken ct);
}
