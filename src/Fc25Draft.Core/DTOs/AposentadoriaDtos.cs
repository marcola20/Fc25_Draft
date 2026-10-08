namespace Fc25Draft.Core.DTOs;

/// <summary>Um jogador na tela de aposentadorias.</summary>
/// <param name="Chance">Chance de anunciar a despedida nesta temporada (0 a 1).</param>
/// <param name="Sorteado">O sorteio da temporada escolheu ele (sugestão de anúncio).</param>
/// <param name="Revelado">A despedida já foi anunciada ao público (falso = decidida, guardada em segredo).</param>
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
    int? AposentadoNaTemporada,
    bool Revelado = false);

/// <summary>
/// Aposentadorias de uma temporada: quem se aposenta agora (decidiu antes), quem já anunciou a despedida nesta
/// temporada, quem já decidiu mas o admin ainda guarda em segredo, as sugestões do sorteio e os já aposentados.
/// <see cref="Anunciados"/> é público (página da virada); <see cref="Guardados"/> só o admin vê.
/// </summary>
public record AposentadoriaPainelDto(
    int Temporada,
    IReadOnlyList<AposentadoriaJogadorDto> ParaAposentar,
    IReadOnlyList<AposentadoriaJogadorDto> Anunciados,
    IReadOnlyList<AposentadoriaJogadorDto> Guardados,
    IReadOnlyList<AposentadoriaJogadorDto> Candidatos,
    IReadOnlyList<AposentadoriaJogadorDto> Aposentados);

/// <summary>Lenda aposentada para o Hall da Fama: onde terminou a carreira e o que fez na liga.</summary>
public record LendaAposentadaDto(
    int PlayerId,
    string Nome,
    string Posicao,
    int UltimaTemporada,
    string? UltimoClube,
    int Overall,
    int Jogos,
    int Gols);

/// <summary>Aposentado para o Editor PES tirar dos clubes no save.</summary>
public record AposentadoPesDto(int PlayerId, string Nome, int? PesId, int Temporada);
