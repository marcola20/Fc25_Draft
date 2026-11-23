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
    DateTime? ResultadoEm);

public record EleicaoApuracaoDto(int Validos, int Brancos, int Abstencoes)
{
    public int Total => Validos + Brancos;
}

public record EleicaoResumoDto(Guid EleicaoId, string Titulo, int Turno, SituacaoEleicao Situacao, DateTime CriadaEm);

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
}
