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

    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }

    public Treinador Treinador { get; set; } = null!;
    public LigaPartida Partida { get; set; } = null!;
}
