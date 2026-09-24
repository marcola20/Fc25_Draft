using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

/// <summary>Escolha automática de um time num draft: quando chega a vez dele, o sistema escolhe pela lista.</summary>
public class DraftAutoPick
{
    public Guid DraftId { get; set; }
    public Guid TeamId { get; set; }
    public DraftAutoPickModo Modo { get; set; }
    public bool Ativo { get; set; }
    public DateTime AtualizadoEm { get; set; }

    public Draft Draft { get; set; } = null!;
    public Team Team { get; set; } = null!;
    public ICollection<DraftAutoPickRodada> Rodadas { get; set; } = new List<DraftAutoPickRodada>();
    public ICollection<DraftAutoPickItem> Itens { get; set; } = new List<DraftAutoPickItem>();
}

/// <summary>Modo posição: qual posição o time quer em cada rodada.</summary>
public class DraftAutoPickRodada
{
    public Guid DraftId { get; set; }
    public Guid TeamId { get; set; }
    public int RoundNumber { get; set; }
    public short PositionId { get; set; }

    public DraftAutoPick Config { get; set; } = null!;
    public Position Position { get; set; } = null!;
}

/// <summary>Jogador numa lista de prioridade. <see cref="PositionId"/> é nulo no modo jogador.</summary>
public class DraftAutoPickItem
{
    public Guid DraftAutoPickItemId { get; set; }
    public Guid DraftId { get; set; }
    public Guid TeamId { get; set; }
    public short? PositionId { get; set; }
    public int PlayerId { get; set; }
    public int Ordem { get; set; }

    public DraftAutoPick Config { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
