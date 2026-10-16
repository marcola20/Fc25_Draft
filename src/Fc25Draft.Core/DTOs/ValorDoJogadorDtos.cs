namespace Fc25Draft.Core.DTOs;

/// <summary>Como o jogador entrou no jogo.</summary>
public enum ParticipouComo
{
    NaoJogou,
    EntrouDoBanco,
    Titular
}

/// <summary>
/// O valor de mercado do jogador logo depois de um jogo do clube atual (preço base de hoje com a variação da
/// participação daquele momento).
/// </summary>
/// <param name="Humor">"😤" quando ficou insatisfeito nesse jogo, "🤝" quando fez as pazes; nulo se nada mudou.</param>
public record ValorDoJogadorPontoDto(
    Guid PartidaId,
    DateTime Data,
    string Adversario,
    int GolsPro,
    int GolsContra,
    ParticipouComo Participou,
    decimal Valor,
    decimal Variacao,
    decimal? Percentual,
    bool Insatisfeito,
    string? Humor,
    // Menos jogos que o mínimo no clube: o valor ainda não mudava.
    bool EmCarencia);

/// <param name="ValorSemVariacao">Preço base de hoje, sem a participação (o valor de quem acabou de chegar).</param>
public record ValorDoJogadorDto(string TimeNome, decimal ValorSemVariacao, IReadOnlyList<ValorDoJogadorPontoDto> Pontos);
