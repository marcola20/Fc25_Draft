using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Um lado da prévia do jogo. Os números de tabela ficam nulos quando a competição não tem tabela.</summary>
public record PreviaTimeDto(
    Guid TimeId,
    string Nome,
    int? Posicao,
    int? Pontos,
    int? Jogos,
    int? GolsPro,
    int? GolsContra,
    // Últimos 5 jogos em todas as competições, do mais antigo para o mais recente.
    IReadOnlyList<ResultadoForma> Forma,
    // Ex.: "4 vitórias seguidas"; e "V"/"E"/"D" para a cor.
    string? Sequencia,
    string? SequenciaTipo,
    string? Artilheiro,
    int ArtilheiroGols,
    NumerosDoTimeDto? Numeros,
    IReadOnlyList<string> Suspensos,
    IReadOnlyList<string> Pendurados);

public record PreviaJogoAnteriorDto(DateTime? Data, string Competicao, string Mandante, int GolsMandante, int GolsVisitante, string Visitante);

/// <summary>Confronto direto em todas as competições, contado do lado do mandante desta partida.</summary>
public record PreviaConfrontoDto(
    int Jogos,
    int VitoriasCasa,
    int Empates,
    int VitoriasFora,
    int GolsCasa,
    int GolsFora,
    IReadOnlyList<PreviaJogoAnteriorDto> Ultimos);

public record PreviaDto(Guid PartidaId, string Competicao, PreviaTimeDto Casa, PreviaTimeDto Fora, PreviaConfrontoDto Confronto)
{
    public bool TemTabela => Casa.Jogos is not null && Fora.Jogos is not null;
}

/// <summary>Raio-x dos dois times antes do jogo.</summary>
public interface IPreviaService
{
    /// <summary>Nulo quando a partida não existe ou já terminou (aí valem as estatísticas do jogo).</summary>
    Task<PreviaDto?> MontarAsync(Guid partidaId, CancellationToken ct);
}
