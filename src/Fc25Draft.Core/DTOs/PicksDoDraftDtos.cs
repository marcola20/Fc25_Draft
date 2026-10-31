namespace Fc25Draft.Core.DTOs;

/// <summary>Uma pick do próximo draft numa proposta: a rodada e o time a que ela pertence pela classificação.</summary>
public record PickRefDto(int Temporada, int Rodada, Guid TimeOriginalId);

/// <summary>Uma pick do draft, com quem é o dono hoje.</summary>
/// <param name="Posicao">Posição na rodada, depois da loteria (nulo antes).</param>
/// <param name="Usada">Já escolheu no draft: não se negocia mais.</param>
/// <param name="ProtegidaAte">Proteção que ainda vale (ou valeu): se cair até essa posição, volta para o protetor.</param>
public record PickDoDraftDto(
    int Temporada,
    int Rodada,
    Guid TimeOriginalId,
    string TimeOriginalNome,
    Guid DonoId,
    string DonoNome,
    int? Posicao = null,
    bool Usada = false,
    int? ProtegidaAte = null,
    string? ProtetorNome = null,
    decimal? Compensacao = null,
    bool? ProtecaoAcionada = null)
{
    public bool Negociada => DonoId != TimeOriginalId;

    /// <summary>"1ª rodada do Vasco" ou, depois da loteria, "#3 · 1ª rodada do Vasco".</summary>
    public string Rotulo => (Posicao is int pos ? $"#{pos} · " : "") + $"{Rodada}ª rodada do {TimeOriginalNome}";

    public PickRefDto Ref => new(Temporada, Rodada, TimeOriginalId);
}

/// <summary>Proteção que quem cede põe na pick: se cair até a posição, volta para ele, que devolve a compensação.</summary>
public record PickProtecaoDto(PickRefDto Pick, int ProtegidaAte, decimal Compensacao);

/// <param name="Temporada">Temporada do próximo draft (a em andamento); nulo se não há.</param>
/// <param name="Aberta">Tem pick que ainda se negocia (antes de ser usada no draft).</param>
/// <param name="Picks">As picks que ainda se negociam e as já usadas do draft em andamento, de todos os drafts abertos.</param>
public record PicksDoProximoDraftDto(int? Temporada, bool Aberta, IReadOnlyList<PickDoDraftDto> Picks);
