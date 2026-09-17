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

    /// <summary>Ano da temporada (ex.: 2010). Agrupa Série A, Série B e Copa disputadas juntas.</summary>
    public int? Temporada { get; set; }

    /// <summary>Divisão da Liga de pontos corridos; nulo para Copa/Supercopa.</summary>
    public Divisao? Divisao { get; set; }

    /// <summary>Série A: últimos que caem direto. Série B: primeiros que sobem direto. Nulo/0 = nenhum.</summary>
    public int? VagasDiretas { get; set; }

    /// <summary>Série A: quem vem logo acima dos rebaixados e joga o playoff. Série B: quem vem logo abaixo dos promovidos.</summary>
    public int? VagasPlayoff { get; set; }

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
