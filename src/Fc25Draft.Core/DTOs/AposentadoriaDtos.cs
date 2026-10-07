namespace Fc25Draft.Core.DTOs;

/// <summary>Um jogador na tela de aposentadorias.</summary>
/// <param name="Chance">Chance de anunciar a despedida nesta temporada (0 a 1).</param>
/// <param name="Sorteado">O sorteio da temporada escolheu ele (sugestão de anúncio).</param>
public record AposentadoriaJogadorDto(
    int PlayerId,
    string Nome,
    string? TimeNome,
    string Posicao,
    int Idade,
    int Overall,
    double Chance,
    bool Sorteado,
    int? UltimaTemporada,
    int? AposentadoNaTemporada);

/// <summary>
/// Aposentadorias de uma temporada: quem se aposenta agora (anunciou antes), quem já anunciou a despedida nesta
/// temporada, as sugestões do sorteio e os já aposentados.
/// </summary>
public record AposentadoriaPainelDto(
    int Temporada,
    IReadOnlyList<AposentadoriaJogadorDto> ParaAposentar,
    IReadOnlyList<AposentadoriaJogadorDto> Anunciados,
    IReadOnlyList<AposentadoriaJogadorDto> Candidatos,
    IReadOnlyList<AposentadoriaJogadorDto> Aposentados);

/// <summary>Aposentado para o Editor PES tirar dos clubes no save.</summary>
public record AposentadoPesDto(int PlayerId, string Nome, int? PesId, int Temporada);
