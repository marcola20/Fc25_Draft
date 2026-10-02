namespace Fc25Draft.Core.Interfaces;

public record BuscaJogadorDto(int PlayerId, string Nome, string Posicao, int Overall, string? TimeNome);

public record BuscaTimeDto(Guid TimeId, string Nome, string? Tecnico);

public record BuscaResultadoDto(IReadOnlyList<BuscaJogadorDto> Jogadores, IReadOnlyList<BuscaTimeDto> Times)
{
    public static BuscaResultadoDto Vazio { get; } = new(Array.Empty<BuscaJogadorDto>(), Array.Empty<BuscaTimeDto>());
    public bool Nada => Jogadores.Count == 0 && Times.Count == 0;
}

/// <summary>Busca do topo do site: jogadores pelo nome e times pelo nome ou pelo técnico.</summary>
public interface IBuscaService
{
    Task<BuscaResultadoDto> BuscarAsync(string termo, CancellationToken ct);
}
