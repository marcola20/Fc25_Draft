namespace Fc25Draft.Core.Entities;

/// <summary>
/// Jogos de um jogador numa competição sem contador (sem escalação registrada), anotados à mão pelo admin.
/// Só vale nas competições sem contagem: nas demais o número sai das escalações.
/// </summary>
public class JogosAnotados
{
    public int JogadorId { get; set; }
    public Guid LigaId { get; set; }
    public Guid TimeId { get; set; }
    public int Jogos { get; set; }
    public DateTime AtualizadoEmUtc { get; set; }

    public Player Jogador { get; set; } = null!;
    public Liga Liga { get; set; } = null!;
    public Team Time { get; set; } = null!;
}
