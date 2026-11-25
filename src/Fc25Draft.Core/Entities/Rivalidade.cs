namespace Fc25Draft.Core.Entities;

/// <summary>
/// Dois treinadores rivais: quando os dois se enfrentam no comando dos clubes (técnico principal dos dois lados),
/// o jogo é um clássico, seja qual for o clube de cada um. A rivalidade segue as pessoas, não os times.
/// O par é guardado em ordem (<see cref="TreinadorAId"/> menor que <see cref="TreinadorBId"/>), para não repetir.
/// </summary>
public class Rivalidade
{
    public Guid RivalidadeId { get; set; }
    public Guid TreinadorAId { get; set; }
    public Guid TreinadorBId { get; set; }

    /// <summary>Nome do clássico, opcional (ex.: "Clássico dos Pedros").</summary>
    public string? Nome { get; set; }

    /// <summary>O prêmio só vale para jogos que começaram depois daqui; o retrospecto conta a história toda.</summary>
    public DateTime CriadaEm { get; set; }

    public Treinador TreinadorA { get; set; } = null!;
    public Treinador TreinadorB { get; set; } = null!;
}

/// <summary>Prêmio pago ao clube do vencedor de um clássico (um por partida).</summary>
public class ClassicoPremio
{
    public Guid PremioId { get; set; }
    public Guid PartidaId { get; set; }
    public Guid? RivalidadeId { get; set; }

    /// <summary>O treinador que venceu e o clube que ele comandava no jogo (quem recebe).</summary>
    public Guid? TreinadorId { get; set; }
    public Guid? TimeId { get; set; }

    public decimal Valor { get; set; }
    public DateTime PagoEm { get; set; }

    public LigaPartida Partida { get; set; } = null!;
    public Rivalidade? Rivalidade { get; set; }
    public Treinador? Treinador { get; set; }
    public Team? Time { get; set; }
}
