namespace Fc25Draft.Core.Entities;

/// <summary>As premiações individuais de estatística (top 3 de cada uma ganha dinheiro para o clube e +1 over).</summary>
public enum CategoriaEstatistica
{
    Artilharia = 1,
    Assistencias = 2,
    CleanSheets = 3
}

/// <summary>
/// Um jogador premiado no top 3 de uma estatística da temporada (Série A, Série B e Copa somadas): o dinheiro vai para o clube dele e o
/// +1 over para ele. Quem já levou over por outra estatística da temporada tem o over repassado a um
/// companheiro, que o treinador do clube escolhe na Minha Área.
/// </summary>
public class PremioEstatistica
{
    public Guid PremioId { get; set; }
    public int Temporada { get; set; }
    public CategoriaEstatistica Categoria { get; set; }

    /// <summary>1, 2 ou 3. Empatados dividem a posição e cada um leva o prêmio inteiro dela.</summary>
    public int Posicao { get; set; }

    /// <summary>Gols, assistências ou clean sheets na temporada.</summary>
    public int Numero { get; set; }

    /// <summary>De onde veio o número: "Série A 9 · Copa 3".</summary>
    public string? Detalhe { get; set; }

    public int PlayerId { get; set; }

    /// <summary>Clube que recebeu o dinheiro (o do jogador quando o prêmio foi dado).</summary>
    public Guid? TimeId { get; set; }
    public decimal Valor { get; set; }
    public DateTime PagoEm { get; set; }

    /// <summary>O jogador já levou over por outra estatística: o over vai para um companheiro.</summary>
    public bool Repasse { get; set; }

    public int? OverParaPlayerId { get; set; }
    public DateTime? OverAplicadoEm { get; set; }

    /// <summary>A mudança de atributos que vai para o save pelo Editor PES; nula se o jogador não tem atributos do jogo.</summary>
    public int? EvolucaoPesId { get; set; }

    public Player Player { get; set; } = null!;
    public Team? Time { get; set; }
    public Player? OverPara { get; set; }
    public EvolucaoPes? Evolucao { get; set; }
}
