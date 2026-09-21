namespace Fc25Draft.Core.DTOs;

/// <param name="TimesNovos">Times que vão escolher, na ordem da 1ª rodada (se não houver sorteio).</param>
public record DraftExpansaoCriarRequest(
    string Nome,
    IReadOnlyList<Guid> TimesNovos,
    int Rodadas,
    bool Serpentina,
    bool SortearOrdem,
    int ProtegidosPorTime = 11,
    int MaxPerdasPorTime = 2,
    decimal FatorCompensacao = 1.2m);

/// <summary>Draft de jogadores livres só para os times novos, depois da expansão.</summary>
public record DraftComplementarRequest(
    string Nome,
    int Rodadas,
    bool Serpentina,
    bool SortearOrdem,
    int? OverallMin = null,
    int? OverallMax = null);

public record DraftExpansaoResumoDto(
    Guid DraftId,
    string Nome,
    int ProtegidosPorTime,
    int MaxPerdasPorTime,
    decimal FatorCompensacao,
    bool ProtecaoEncerrada,
    int TotalEscolhas,
    int EscolhasFeitas,
    IReadOnlyList<DraftExpansaoTimeExistenteDto> TimesExistentes,
    IReadOnlyList<DraftExpansaoTimeNovoDto> TimesNovos,
    IReadOnlyList<DraftExpansaoEscolhaDto> Escolhas,
    decimal CaixaMedioExistentes)
{
    public bool Concluido => TotalEscolhas > 0 && EscolhasFeitas == TotalEscolhas;
}

public record DraftExpansaoTimeExistenteDto(
    Guid TeamId,
    string TeamName,
    string? OwnerName,
    int Elenco,
    int Protegidos,
    bool ProtecaoAutomatica,
    int Perdas,
    decimal CompensacaoTotal,
    int? MinimoTemporario);

public record DraftExpansaoTimeNovoDto(
    Guid TeamId,
    string TeamName,
    string? OwnerName,
    int Elenco,
    int Escolhas,
    decimal Caixa);

public record DraftExpansaoEscolhaDto(
    int OverallPick,
    int Rodada,
    string TimeNovo,
    string? Jogador,
    int? Overall,
    string? TimeOrigem,
    decimal? Compensacao);

public record DraftProtecaoTimeDto(
    Guid DraftId,
    string DraftNome,
    Guid TeamId,
    string TeamName,
    int ProtegidosPorTime,
    int MaxPerdasPorTime,
    bool Aberta,
    IReadOnlyList<DraftProtecaoJogadorDto> Elenco)
{
    /// <summary>Quantos o time precisa proteger: o limite, ou o elenco inteiro se for menor.</summary>
    public int Exigidos => Math.Min(ProtegidosPorTime, Elenco.Count);
}

public record DraftProtecaoJogadorDto(
    int PlayerId,
    string Nome,
    string Posicao,
    int Overall,
    int? Idade,
    bool Protegido,
    bool ProtecaoAutomatica);
