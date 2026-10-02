namespace Fc25Draft.Core.Interfaces;

/// <summary>Avisos do time (proposta recebida, lance superado…), do sino do topo e da Minha Área.</summary>
public interface IAvisosService
{
    Task<int> ContarNaoLidosAsync(Guid teamId, CancellationToken ct);

    /// <summary>Os avisos mais recentes do time, do mais novo para o mais antigo.</summary>
    Task<IReadOnlyList<AvisoDto>> ListarAsync(Guid teamId, int limite, CancellationToken ct);

    Task MarcarTodosComoLidosAsync(Guid teamId, CancellationToken ct);
}

public record AvisoDto(Guid AvisoId, string Tipo, string Texto, string? Link, DateTime CriadoEmUtc, bool Lido);
