using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.DTOs;

/// <summary>Meta de um time numa competição e como ela está indo.</summary>
/// <param name="Andamento">Onde o time está agora: "Hoje: 4º", "Nas quartas", "Terminou em 3º".</param>
/// <param name="DentroDaMeta">Liga em andamento: a posição de hoje cumpre a meta. Nulo na Copa ou antes do 1º jogo.</param>
public record DiretoriaMetaDto(
    Guid MetaId,
    Guid LigaId,
    string LigaNome,
    TipoCompetition Tipo,
    Divisao? Divisao,
    string Meta,
    int? MetaPosicao,
    FasePremiacao? MetaFase,
    int? Pote,
    int? PosicaoEsperada,
    double? Nota,
    double? MediaHistorico,
    double? ForcaXI,
    SituacaoMeta Situacao,
    string? Andamento,
    bool? DentroDaMeta,
    bool AjustadaPeloAdmin);

public record DiretoriaPontoDto(
    Guid PartidaId,
    DateTime Data,
    string Competicao,
    Guid AdversarioId,
    string AdversarioNome,
    int GolsPro,
    int GolsContra,
    double Variacao,
    double Valor);

/// <param name="UltimaVariacao">Quanto a confiança mudou no último jogo; nulo antes do 1º jogo.</param>
public record DiretoriaTimeDto(
    Guid TimeId,
    string TimeNome,
    Divisao? Divisao,
    double Confianca,
    FaixaConfianca Faixa,
    double? UltimaVariacao,
    IReadOnlyList<DiretoriaMetaDto> Metas,
    IReadOnlyList<DiretoriaPontoDto> Historico);

/// <summary>Diretoria de todos os times numa temporada.</summary>
public record DiretoriaPainelDto(
    int Temporada,
    bool MetasGeradas,
    IReadOnlyList<DiretoriaTimeDto> Times);

/// <summary>Time que mudou de faixa num jogo da rodada.</summary>
public record DiretoriaMudancaDto(string TimeNome, FaixaConfianca Antes, FaixaConfianca Depois, double Confianca);

/// <summary>Time abaixo de "estável" depois da rodada.</summary>
public record DiretoriaNaCordaDto(string TimeNome, FaixaConfianca Faixa, double Confianca);

/// <summary>A diretoria no resumo da rodada: quem mudou de faixa e quem está mais perto da demissão.</summary>
public record DiretoriaResumoRodadaDto(
    IReadOnlyList<DiretoriaMudancaDto> Mudancas,
    IReadOnlyList<DiretoriaNaCordaDto> NaCorda)
{
    public bool Vazio => Mudancas.Count == 0 && NaCorda.Count == 0;
}

/// <summary>Quem recebe bônus da diretoria numa competição, antes de creditar no caixa.</summary>
public record DiretoriaBonusPreviaDto(
    Guid LigaId,
    string LigaNome,
    TipoCompetition Tipo,
    Divisao? Divisao,
    bool Encerrada,
    bool JaPago,
    DateTime? PagoEm,
    string? Impedimento,
    IReadOnlyList<PremiacaoLinhaDto> Linhas)
{
    public decimal Total => Linhas.Sum(l => l.Valor);

    public bool PodePagar => Encerrada && !JaPago && Impedimento is null && Linhas.Count > 0;
}
