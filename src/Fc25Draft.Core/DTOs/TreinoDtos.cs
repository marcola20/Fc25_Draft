using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.DTOs;

/// <summary>Um jogador do elenco na tela do treino.</summary>
/// <param name="PorQueNaoTreina">Nulo = pode treinar.</param>
/// <param name="Foco">O foco escolhido nesta semana (nulo = não está no treino).</param>
/// <param name="GanhosNaTemporada">Atributos que já subiram no treino nesta temporada.</param>
public record TreinoJogadorDto(
    int PlayerId,
    string Nome,
    string Posicao,
    int Overall,
    int? Idade,
    bool Goleiro,
    string? PorQueNaoTreina,
    FocoDoTreino? Foco,
    int GanhosNaTemporada);

/// <summary>O treino que aconteceu (ou não) numa semana.</summary>
public record TreinoResultadoDto(
    DateTime Semana,
    int PlayerId,
    string Nome,
    FocoDoTreino Foco,
    string? Atributo,
    int? OverallAntes,
    int? OverallDepois,
    string? SemEfeito,
    DateTime? AplicadoEm);

/// <param name="Semana">Segunda-feira da semana (Brasília).</param>
/// <param name="FechaEm">Quando o treino acontece (segunda seguinte, 0h de Brasília).</param>
/// <param name="UltimaSemana">O que aconteceu no treino da semana passada.</param>
public record TreinoDoTimeDto(
    DateTime Semana,
    DateTime FechaEm,
    IReadOnlyList<TreinoJogadorDto> Jogadores,
    IReadOnlyList<TreinoResultadoDto> UltimaSemana)
{
    public int Escolhidos => Jogadores.Count(j => j.Foco is not null);
}

public record TreinoEscolhaRequest(int PlayerId, FocoDoTreino Foco);
