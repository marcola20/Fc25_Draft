namespace Fc25Draft.Core.Entities;

/// <summary>
/// Cláusula de recompra de uma venda entre times: quem vendeu pode comprar o jogador de volta pelo preço combinado até
/// o fim da temporada, enquanto ele estiver no clube que comprou.
/// </summary>
public class DireitoDeRecompra
{
    public Guid DireitoId { get; set; }
    public int PlayerId { get; set; }

    /// <summary>Quem vendeu e pode recomprar.</summary>
    public Guid VendedorId { get; set; }

    /// <summary>Quem comprou (o jogador precisa seguir nele para a recompra valer).</summary>
    public Guid CompradorId { get; set; }

    public decimal Preco { get; set; }

    /// <summary>Temporada em que a venda aconteceu: a recompra vale até a virada.</summary>
    public int? Temporada { get; set; }

    /// <summary>A venda que criou a cláusula.</summary>
    public Guid OfferId { get; set; }

    public DateTime CriadoEm { get; set; }

    /// <summary>Quando foi exercida (nulo = não foi).</summary>
    public DateTime? ExercidaEm { get; set; }

    public Player Player { get; set; } = null!;
    public Team Vendedor { get; set; } = null!;
    public Team Comprador { get; set; } = null!;
}
