namespace Fc25Draft.Core.Entities;

/// <summary>
/// Emoji que um treinador deixou num jogo (🔥, 😂...). Cada treinador marca cada emoji
/// uma vez só; clicar de novo tira.
/// </summary>
public class PartidaReacao
{
    public Guid PartidaId { get; set; }
    public Guid TreinadorId { get; set; }
    public string Emoji { get; set; } = null!;
    public DateTime CriadaEm { get; set; }

    public LigaPartida Partida { get; set; } = null!;
    public Treinador Treinador { get; set; } = null!;
}
