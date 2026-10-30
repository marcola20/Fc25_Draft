namespace Fc25Draft.Core.Entities;

/// <summary>
/// O palpite de uma pessoa para um jogo. É da pessoa, não do clube: treinador e auxiliar
/// palpitam separado, e quem está sem clube joga do mesmo jeito.
/// </summary>
public class BolaoPalpite
{
    public Guid PalpiteId { get; set; }
    public Guid TreinadorId { get; set; }
    public Guid PartidaId { get; set; }

    public int GolsCasa { get; set; }
    public int GolsFora { get; set; }

    /// <summary>
    /// Aposta G/A: um jogador do mandante que faz gol ou dá assistência. Opcional, e só vale com o
    /// mandante marcando no palpite — quem chutou 0 para ele aposta no clean sheet do visitante.
    /// </summary>
    public int? JogadorCasaId { get; set; }

    /// <summary>A mesma aposta G/A, do lado do visitante.</summary>
    public int? JogadorForaId { get; set; }

    /// <summary>
    /// Jogo que precisa de vencedor (mata-mata, Supercopa, jogo decisivo) com empate no palpite: o time
    /// que a pessoa acha que passa nos pênaltis. Opcional.
    /// </summary>
    public Guid? PenaltisVencedorId { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }

    public Treinador Treinador { get; set; } = null!;
    public LigaPartida Partida { get; set; } = null!;
    public Player? JogadorCasa { get; set; }
    public Player? JogadorFora { get; set; }
}
