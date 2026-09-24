using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

/// <summary>
/// Rodada do próximo draft, definida pelo admin antes de o draft existir (ex.: rodada 1 até 87,
/// rodada 2 até 84). Serve para os times montarem a escolha automática com antecedência.
/// </summary>
public class DraftPlanejadoRodada
{
    public int RoundNumber { get; set; }
    public int? OverallMin { get; set; }
    public int? OverallMax { get; set; }
}

/// <summary>Escolha automática montada para o próximo draft. Vira <see cref="DraftAutoPick"/> quando o draft é gerado.</summary>
public class DraftAutoPickPrevia
{
    public Guid TeamId { get; set; }
    public DraftAutoPickModo Modo { get; set; }
    public bool Ativo { get; set; }
    public DateTime AtualizadoEm { get; set; }

    public Team Team { get; set; } = null!;
    public ICollection<DraftAutoPickPreviaRodada> Rodadas { get; set; } = new List<DraftAutoPickPreviaRodada>();
    public ICollection<DraftAutoPickPreviaItem> Itens { get; set; } = new List<DraftAutoPickPreviaItem>();
}

public class DraftAutoPickPreviaRodada
{
    public Guid TeamId { get; set; }
    public int RoundNumber { get; set; }
    public short PositionId { get; set; }

    public DraftAutoPickPrevia Previa { get; set; } = null!;
    public Position Position { get; set; } = null!;
}

public class DraftAutoPickPreviaItem
{
    public Guid DraftAutoPickPreviaItemId { get; set; }
    public Guid TeamId { get; set; }
    public short? PositionId { get; set; }
    public int PlayerId { get; set; }
    public int Ordem { get; set; }

    public DraftAutoPickPrevia Previa { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
