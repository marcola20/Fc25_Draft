namespace Fc25Draft.Core.Entities;

/// <summary>Time num pote do sorteio da Copa. Cada pote distribui seus times igualmente entre os grupos.</summary>
public class LigaCopaPote
{
    public Guid Id { get; set; }
    public Guid LigaId { get; set; }
    public Guid TimeId { get; set; }

    /// <summary>Número do pote (1, 2, 3...).</summary>
    public int Pote { get; set; }

    public Liga Liga { get; set; } = null!;
    public Team Time { get; set; } = null!;
}
