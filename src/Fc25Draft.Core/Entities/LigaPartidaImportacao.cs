namespace Fc25Draft.Core.Entities;

/// <summary>
/// Resultado de uma partida importado do PES 2021 (JSON gerado pelo Auto_PES21).
/// Guarda o JSON bruto — inclusive notas e estatísticas, que ainda não têm tela —
/// e marca a partida como importada: reenviar o mesmo jogo substitui os eventos.
/// </summary>
public class LigaPartidaImportacao
{
    public Guid PartidaId { get; set; }
    public string? Video { get; set; }
    public string Json { get; set; } = null!;
    public DateTime ImportadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }

    public LigaPartida Partida { get; set; } = null!;
}
