using System;

namespace Fc25Draft.Core.Entities;

public class Player
{
    public int PlayerId { get; set; }
    public Guid PlayerGuid { get; set; }
    public string Name { get; set; } = null!;
    public int? Age { get; set; }

    /// <summary>País (nome em português, ver <c>Paises</c>). Vem da nacionalidade do PES; o admin pode trocar.</summary>
    public string? Pais { get; set; }
    public int Overall { get; set; }
    public short PositionId { get; set; }
    public Guid? CurrentTeamId { get; set; }
    public Guid? QuickSellTeamId { get; set; }
    public int? QuickSellOldOverall { get; set; }
    public int? QuickSellNewOverall { get; set; }

    // ── Aposentadoria ──
    /// <summary>Anunciou que esta temporada é a última dele; se aposenta na virada seguinte.</summary>
    public int? UltimaTemporada { get; set; }
    public DateTime? DespedidaAnunciadaEm { get; set; }
    /// <summary>Aposentado: a última temporada que jogou. Não volta ao mercado nem ao draft; fica no histórico.</summary>
    public int? AposentadoNaTemporada { get; set; }
    public DateTime? AposentadoEm { get; set; }
    /// <summary>Clube em que estava ao se aposentar (para desfazer).</summary>
    public Guid? TimeAoSeAposentar { get; set; }

    public Position Position { get; set; } = null!;
    public PlayerAtributos? Atributos { get; set; }
    public ICollection<DraftPick> DraftPicks { get; set; } = new List<DraftPick>();
    public ICollection<TeamRoster> TeamRosters { get; set; } = new List<TeamRoster>();
    public Team? CurrentTeam { get; set; }
    public ICollection<MarketItem> MarketItems { get; set; } = new List<MarketItem>();
}
