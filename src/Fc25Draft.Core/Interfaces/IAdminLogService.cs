namespace Fc25Draft.Core.Interfaces;

/// <summary>Registro das ações do admin no mercado (ajuste de caixa, venda, troca, cancelamentos…).</summary>
public interface IAdminLogService
{
    /// <summary>As ações mais recentes, da mais nova para a mais antiga, com os ids trocados por nomes.</summary>
    Task<IReadOnlyList<AdminLogItemDto>> ListarAsync(int limite, CancellationToken ct);
}

public record AdminLogDetalheDto(string Campo, string Valor);

public record AdminLogItemDto(DateTime CriadoEmUtc, string Acao, string QuemFez, IReadOnlyList<AdminLogDetalheDto> Detalhes);
