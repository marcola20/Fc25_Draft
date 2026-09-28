namespace Fc25Draft.Core.Entities;

/// <summary>
/// Uma mensagem do chat do telão de um jogo. Fica gravada para quem assistir depois
/// ver a resenha de quem acompanhou a estreia.
/// </summary>
public class PartidaChatMensagem
{
    public Guid MensagemId { get; set; }
    public Guid PartidaId { get; set; }
    public Guid TreinadorId { get; set; }

    public string Texto { get; set; } = null!;
    public DateTime EnviadaEm { get; set; }

    public LigaPartida Partida { get; set; } = null!;
    public Treinador Treinador { get; set; } = null!;
}
