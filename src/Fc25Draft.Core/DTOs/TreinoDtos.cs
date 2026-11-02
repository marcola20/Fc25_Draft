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
    int GanhosNaTemporada,
    GrupoDePosicao Grupo = GrupoDePosicao.Meio);

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
    DateTime? AplicadoEm,
    string? AtributoDoPadrinho = null);

/// <param name="Semana">Segunda-feira da semana (Brasília).</param>
/// <param name="FechaEm">Quando o treino acontece (segunda seguinte, 0h de Brasília).</param>
/// <param name="UltimaSemana">O que aconteceu no treino da semana passada.</param>
public record TreinoDoTimeDto(
    DateTime Semana,
    DateTime FechaEm,
    IReadOnlyList<TreinoJogadorDto> Jogadores,
    IReadOnlyList<TreinoResultadoDto> UltimaSemana,
    PadrinhoDto? Padrinho = null,
    IReadOnlyList<PadrinhoCandidatoDto>? Veteranos = null,
    bool PodeApadrinhar = false,
    int Vagas = TreinoCriterios.JogadoresPorSemana,
    int IdadeMaxima = TreinoCriterios.IdadeMaxima,
    bool TreinadorDeGoleiros = false)
{
    public int Escolhidos => Jogadores.Count(j => j.Foco is not null);
}

public record TreinoEscolhaRequest(int PlayerId, FocoDoTreino Foco);

/// <summary>O par de padrinho e afilhado do time na temporada.</summary>
/// <param name="Treinos">Semanas que o afilhado já treinou com o padrinho (o bônus da virada pede PadrinhoCriterios.TreinosParaOBonus).</param>
public record PadrinhoDto(int PadrinhoId, string PadrinhoNome, int AfilhadoId, string AfilhadoNome, int Treinos);

/// <summary>Veterano do elenco que pode ser padrinho.</summary>
public record PadrinhoCandidatoDto(int PlayerId, string Nome, string Posicao, int? Idade, int Overall, GrupoDePosicao Grupo);

public record ApadrinharRequest(int PadrinhoId, int AfilhadoId);

/// <summary>O treino de um time no painel do admin.</summary>
/// <param name="PodemTreinar">Jogadores do elenco que podem treinar.</param>
public record TreinoDoTimeNaLigaDto(Guid TeamId, string TeamName, TreinoDoTimeDto Treino, int PodemTreinar)
{
    /// <summary>Quantos dá para escolher nesta semana (as vagas, ou menos se não há tanta gente que pode).</summary>
    public int Cabem => Math.Min(Treino.Vagas, PodemTreinar);
    public bool Montou => Treino.Escolhidos >= Cabem;
}

/// <summary>O treino da semana de todos os times (painel do admin).</summary>
/// <param name="EvolucoesPendentes">Evoluções do treino que ainda não foram para o save do PES.</param>
public record TreinoDaLigaDto(DateTime Semana, DateTime FechaEm, IReadOnlyList<TreinoDoTimeNaLigaDto> Times, int EvolucoesPendentes);
