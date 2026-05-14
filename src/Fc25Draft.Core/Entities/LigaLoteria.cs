namespace Fc25Draft.Core.Entities;

public class LigaLoteria
{
    public Guid LoteraiaId { get; set; }
    public Guid LigaId { get; set; }
    public string LigaNome { get; set; } = "";
    public bool IsFinished { get; set; }
    public DateTime CriadoEm { get; set; }

    public List<LigaLoteriaPick> Picks { get; set; } = [];
}

public class LigaLoteriaPick
{
    public Guid PickId { get; set; }
    public Guid LoteraiaId { get; set; }
    public int Rodada { get; set; }
    public int PickNumero { get; set; }
    public Guid TimeId { get; set; }
    public string TimeNome { get; set; } = "";
    public int Posicao { get; set; }

    public LigaLoteria Loteria { get; set; } = null!;
}
