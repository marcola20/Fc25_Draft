namespace Fc25Draft.Core.DTOs;

/// <summary>Jogador do site com o ID do PES ligado a ele (nulo = sem ligação). Usado pelo Editor PES.</summary>
public record LigacaoPesDto(
    int PlayerId,
    string Name,
    short PositionId,
    int? Age,
    int Overall,
    string? TeamName,
    int? PesId);

/// <summary>Ligação conferida no Editor PES. PesId nulo = desligar (os atributos ficam, só perdem o ID).</summary>
public record DefinirLigacaoPesDto(int PlayerId, int? PesId);

public record ResultadoLigacoesPesDto(int Ligados, int Desligados, int Iguais, int Atualizados = 0);

/// <summary>Evolução de atributos feita no site e ainda não gravada no save do PES.
/// Mudancas = quanto cada atributo mudou, na ordem de AtributosPes.Todos.</summary>
public record EvolucaoPesDto(
    int Id,
    int PlayerId,
    int? PesId,
    string Nome,
    string Motivo,
    DateTime CriadaEmUtc,
    int OverallAntes,
    int OverallDepois,
    int[] Mudancas);

/// <summary>Atributos como estão no save do PES (25 valores, na ordem de AtributosPes.Todos).</summary>
public record AtributosDoJogoDto(int PlayerId, int[] Atributos, int? PosicaoPes = null, int? PeFracoUso = null,
    int? PeFracoPrecisao = null);

/// <summary>
/// O Editor PES gravou o save: <c>Aplicadas</c> = evoluções que foram para o jogo; <c>Atributos</c> = como ficaram
/// no jogo os jogadores mexidos (o site passa a ter esses atributos, mais as evoluções ainda pendentes).
/// </summary>
public record SincronizarPesDto(List<int> Aplicadas, List<AtributosDoJogoDto> Atributos);

public record MudancaOverallDto(int PlayerId, string Nome, int Antes, int Depois, double Formula);

public record ResultadoSincronizacaoPesDto(int Aplicadas, int Atualizados, IReadOnlyList<MudancaOverallDto> Overalls,
    IReadOnlyList<int> Ignorados);
