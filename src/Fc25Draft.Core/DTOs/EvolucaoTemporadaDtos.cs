namespace Fc25Draft.Core.DTOs;

/// <summary>Um jogador na prévia (ou no resultado) da evolução de fim de temporada.</summary>
/// <param name="Pontos">Pontos da evolução (idade + desempenho, com o limite de queda): ± em cada atributo afetado.</param>
/// <param name="Variacao">Quanto o overall mudou pela fórmula, com os atributos novos.</param>
/// <param name="Atributos">"Finalização +2, Drible +2…"; vazio se nada mudou.</param>
public record EvolucaoLinhaDto(
    int PlayerId,
    string Nome,
    string? TimeNome,
    string Posicao,
    int Idade,
    int OverallAntes,
    int OverallDepois,
    int Pontos,
    int Variacao,
    int Curva,
    int Desempenho,
    int JogosDoClube,
    int Titular,
    decimal? NotaMedia,
    string Explicacao,
    string Atributos = "",
    // Explosão (+) ou queda de rendimento (−) sorteada; 0 = nenhuma.
    int Surpresa = 0);

/// <summary>A última evolução de fim de temporada de um jogador, com a conta explicada (para a ficha).</summary>
public record VariacaoJogadorDto(
    int Temporada,
    int Pontos,
    int Surpresa,
    int OverallAntes,
    int OverallDepois,
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
    // Pelos pontos da evolução (o overall pode nem mexer com poucos pontos).
    public int Sobem => Linhas.Count(l => l.Pontos > 0);
    public int Caem => Linhas.Count(l => l.Pontos < 0);
    public int Mantem => Linhas.Count(l => l.Pontos == 0);
}
