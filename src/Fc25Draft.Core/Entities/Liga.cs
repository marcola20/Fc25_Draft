using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

public class Liga
{
    public Guid LigaId { get; set; }
    public string Nome { get; set; } = null!;
    public int TotalRodadas { get; set; } = 8;
    public DateTime DataInicio { get; set; }
    public DateTime DataFim { get; set; }
    public LigaStatus Status { get; set; } = LigaStatus.Criada;
    public TipoCompetition Tipo { get; set; } = TipoCompetition.Liga;
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }

    /// <summary>Time campeão, definido quando a competição é encerrada.</summary>
    public Guid? CampeaoTimeId { get; set; }
    public Team? Campeao { get; set; }

    public ICollection<LigaRodada> Rodadas { get; set; } = new List<LigaRodada>();
    public ICollection<LigaClassificacao> Classificacoes { get; set; } = new List<LigaClassificacao>();
    public ICollection<LigaPunicao> Punicoes { get; set; } = new List<LigaPunicao>();
    public ICollection<LigaKnockoutJogo> KnockoutJogos { get; set; } = new List<LigaKnockoutJogo>();
    public ICollection<LigaGrupoTime> Grupos { get; set; } = new List<LigaGrupoTime>();
    public ICollection<LigaTime> Times { get; set; } = new List<LigaTime>();
}
