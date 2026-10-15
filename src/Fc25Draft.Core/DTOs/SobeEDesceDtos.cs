namespace Fc25Draft.Core.DTOs;

/// <summary>
/// Um jogador no Sobe e Desce: valor de mercado no começo do período e hoje. A diferença vem da participação no
/// clube (overall, idade e posição são os de hoje nas duas pontas).
/// </summary>
public record SobeEDesceJogadorDto(
    int PlayerId,
    string Nome,
    Guid TimeId,
    string TimeNome,
    string Posicao,
    int Overall,
    decimal ValorAntes,
    decimal ValorAgora,
    decimal VariacaoAntes,
    decimal VariacaoAgora,
    decimal? ParticipacaoAntes,
    decimal? ParticipacaoAgora,
    bool InsatisfeitoAntes,
    bool InsatisfeitoAgora,
    // Menos de 3 jogos no clube naquele dia: o valor ainda não mudava.
    bool CarenciaAntes)
{
    public decimal Diferenca => ValorAgora - ValorAntes;
}

/// <param name="Desde">Começo do período (UTC).</param>
public record SobeEDesceDto(
    DateTime Desde,
    IReadOnlyList<SobeEDesceJogadorDto> Valorizados,
    IReadOnlyList<SobeEDesceJogadorDto> Desvalorizados,
    IReadOnlyList<SobeEDesceJogadorDto> Insatisfeitos);
