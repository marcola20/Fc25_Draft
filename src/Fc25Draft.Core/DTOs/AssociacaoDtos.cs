using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.DTOs;

public record ChapaMembroDto(
    CargoAssociacao Cargo,
    Guid TreinadorId,
    string Nome,
    string? TimeAtual);

public record ChapaDto(
    Guid ChapaId,
    int Numero,
    string Nome,
    string Plano,
    IReadOnlyList<ChapaMembroDto> Membros,
    /// <summary>Só no resultado (ou para o admin na apuração).</summary>
    int? Votos)
{
    public bool Completa => Membros.Select(m => m.Cargo).Distinct().Count() == Enum.GetValues<CargoAssociacao>().Length;
}

public record EleicaoDto(
    Guid EleicaoId,
    string Titulo,
    int Turno,
    bool Desempate,
    SituacaoEleicao Situacao,
    DateTime? InscricoesAte,
    DateTime? AbreEm,
    DateTime? FechaEm,
    string Edital,
    string Comissao,
    IReadOnlyList<ChapaDto> Chapas,
    int Eleitores,
    int Votaram,
    /// <summary>Contagem: só no resultado (ou para o admin na apuração).</summary>
    EleicaoApuracaoDto? Apuracao,
    Guid? ChapaVencedoraId,
    DateTime CriadaEm,
    DateTime? ResultadoEm,
    /// <summary>O que o resultado decide (no resultado; para o admin também na apuração, como prévia).</summary>
    DesfechoEleicaoDto? Desfecho)
{
    public string Etapa => CargosAssociacao.Etapa(Turno, Desempate);
}

/// <summary>O que sai da contagem de uma etapa.</summary>
public enum TipoDesfecho
{
    /// <summary>Uma chapa eleita (mais de 50% dos válidos, ou pela Presidência no desempate).</summary>
    Eleita,
    /// <summary>Ninguém passou de 50%: 2º turno com as duas classificadas.</summary>
    SegundoTurno,
    /// <summary>Empate que decide classificação ou vencedor: votação de desempate só com as empatadas.</summary>
    Desempate,
    /// <summary>O desempate empatou de novo: a Presidência da CBFV escolhe.</summary>
    DecisaoPresidencia,
    /// <summary>A Presidência já decidiu quem avança ao 2º turno.</summary>
    Decidida,
    /// <summary>Nenhum voto válido.</summary>
    SemVotos
}

/// <param name="Garantidas">Chapas que já seguem (ao 2º turno ou eleita), sem depender do empate.</param>
/// <param name="Empatadas">Chapas da votação de desempate ou da escolha da Presidência.</param>
/// <param name="Vagas">Quantas das empatadas seguem.</param>
public record DesfechoEleicaoDto(
    TipoDesfecho Tipo,
    string Texto,
    IReadOnlyList<Guid> Garantidas,
    IReadOnlyList<Guid> Empatadas,
    int Vagas);

public record EleicaoApuracaoDto(int Validos, int Brancos, int Abstencoes)
{
    public int Total => Validos + Brancos;
}

public record EleicaoResumoDto(Guid EleicaoId, string Titulo, int Turno, bool Desempate, SituacaoEleicao Situacao, DateTime CriadaEm)
{
    public string Etapa => CargosAssociacao.Etapa(Turno, Desempate);
}

public record EleicaoEleitorDto(Guid TreinadorId, string Nome, PapelTreinador? Papel, string? Time, bool Votou);

public record EleicaoSalvarRequest(
    string Titulo,
    DateTime? InscricoesAte,
    DateTime? AbreEm,
    DateTime? FechaEm,
    string? Edital,
    string? Comissao);

public record ChapaSalvarRequest(
    string Nome,
    string? Plano,
    IReadOnlyDictionary<CargoAssociacao, Guid> Membros);

/// <summary>A diretoria em exercício: a chapa eleita na última eleição com resultado.</summary>
public record DiretoriaAssociacaoDto(Guid EleicaoId, string Eleicao, DateTime PosseEm, ChapaDto Chapa);

public static class CargosAssociacao
{
    public static string Nome(CargoAssociacao c) => c switch
    {
        CargoAssociacao.Presidente => "Presidente",
        CargoAssociacao.VicePresidente => "Vice-presidente",
        CargoAssociacao.SecretarioGeral => "Secretário-geral",
        CargoAssociacao.DiretorCompeticoes => "Diretor de Competições",
        CargoAssociacao.DiretorEtica => "Diretor de Ética e Disciplina",
        _ => c.ToString()
    };

    public static string Atribuicao(CargoAssociacao c) => c switch
    {
        CargoAssociacao.Presidente => "Representação máxima da Associação",
        CargoAssociacao.VicePresidente => "Substitui e auxilia a Presidência",
        CargoAssociacao.SecretarioGeral => "Registros, documentos e atas",
        CargoAssociacao.DiretorCompeticoes => "Relação com o calendário (consultivo)",
        CargoAssociacao.DiretorEtica => "Integridade dentro da Associação",
        _ => ""
    };

    public static IReadOnlyList<CargoAssociacao> Todos { get; } = Enum.GetValues<CargoAssociacao>();

    /// <summary>"1º turno", "2º turno", "desempate do 1º turno"...</summary>
    public static string Etapa(int turno, bool desempate) => desempate ? $"desempate do {turno}º turno" : $"{turno}º turno";
}
