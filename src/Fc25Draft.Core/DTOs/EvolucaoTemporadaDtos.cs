namespace Fc25Draft.Core.DTOs;

/// <summary>Um jogador na prévia (ou no resultado) da evolução de fim de temporada.</summary>
public record EvolucaoLinhaDto(
    int PlayerId,
    string Nome,
    string? TimeNome,
    string Posicao,
    int Idade,
    int OverallAntes,
    int OverallDepois,
    int Variacao,
    int Curva,
    int Desempenho,
    int JogosDoClube,
    int Titular,
    decimal? NotaMedia,
    string Explicacao);

/// <summary>Prévia da evolução de uma temporada, ou o que foi aplicado.</summary>
/// <param name="PodeDesfazer">Aplicada e nenhuma mudança foi gravada no jogo ainda pelo Editor PES.</param>
public record EvolucaoPreviaDto(
    int Temporada,
    DateTime? AplicadaEm,
    bool PodeDesfazer,
    IReadOnlyList<EvolucaoLinhaDto> Linhas)
{
    public bool JaAplicada => AplicadaEm is not null;
    public int Sobem => Linhas.Count(l => l.Variacao > 0);
    public int Caem => Linhas.Count(l => l.Variacao < 0);
    public int Mantem => Linhas.Count(l => l.Variacao == 0);
}
