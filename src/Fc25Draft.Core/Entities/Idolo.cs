namespace Fc25Draft.Core.Entities;

/// <summary>Como o ídolo deixou o clube.</summary>
public enum SaidaDoIdolo
{
    Vendido = 1,
    Troca = 2,
    Aposentou = 3,
    DraftDeExpansao = 4,
    Outra = 5
}

/// <summary>
/// Jogador que virou ídolo de um clube (regras em <see cref="Utilities.Idolos"/>). Uma vez ídolo, sempre ídolo:
/// a linha fica mesmo depois que ele sai. Os números são da passagem dele pelo clube e são atualizados pela rotina.
/// </summary>
public class Idolo
{
    public Guid IdoloId { get; set; }
    public int JogadorId { get; set; }
    public Guid TimeId { get; set; }

    public int Jogos { get; set; }
    public int Gols { get; set; }
    public int Assistencias { get; set; }

    /// <summary>Títulos que ganhou pelo clube (Série A, Série B, Copa).</summary>
    public int Titulos { get; set; }

    /// <summary>Por que é ídolo, separado por " | ": "36 gols | Artilheiro do título · Copa do Brasil 2010".</summary>
    public string Motivos { get; set; } = "";

    public DateTime ReconhecidoEm { get; set; }

    /// <summary>Quando deixou o clube pela primeira vez depois de virar ídolo (ou já tinha saído ao ser reconhecido).</summary>
    public DateTime? SaiuEm { get; set; }
    public SaidaDoIdolo? Saida { get; set; }

    /// <summary>
    /// A saída custou confiança da diretoria: vendido ou trocado depois de já ser ídolo. Quem já tinha saído quando
    /// a regra entrou (ou quando foi reconhecido) não tira nada.
    /// </summary>
    public bool PerdeuConfianca { get; set; }

    public Player Jogador { get; set; } = null!;
    public Team Time { get; set; } = null!;
}
