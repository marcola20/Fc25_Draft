namespace Fc25Draft.Core.DTOs;

/// <summary>Uma pick do próximo draft numa proposta: a rodada e o time a que ela pertence pela classificação.</summary>
public record PickRefDto(int Temporada, int Rodada, Guid TimeOriginalId);

/// <summary>Uma pick do próximo draft, com quem é o dono hoje.</summary>
public record PickDoDraftDto(int Temporada, int Rodada, Guid TimeOriginalId, string TimeOriginalNome, Guid DonoId, string DonoNome)
{
    public bool Negociada => DonoId != TimeOriginalId;

    /// <summary>"1ª rodada do Vasco".</summary>
    public string Rotulo => $"{Rodada}ª rodada do {TimeOriginalNome}";

    public PickRefDto Ref => new(Temporada, Rodada, TimeOriginalId);
}

/// <param name="Temporada">Temporada cuja classificação define o próximo draft; nulo se não há.</param>
/// <param name="Aberta">Dá para negociar (a loteria desse draft ainda não aconteceu).</param>
public record PicksDoProximoDraftDto(int? Temporada, bool Aberta, IReadOnlyList<PickDoDraftDto> Picks);
