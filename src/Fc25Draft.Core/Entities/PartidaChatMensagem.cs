namespace Fc25Draft.Core.Entities;

/// <summary>
/// Uma mensagem do chat do telão de um jogo — ou do telão de um mosaico. Fica gravada para quem
/// assistir depois ver a resenha de quem acompanhou a estreia. Tem sempre um dos dois: o jogo ou o mosaico.
/// </summary>
public class PartidaChatMensagem
{
    public Guid MensagemId { get; set; }
    public Guid? PartidaId { get; set; }
    public Guid? MosaicoId { get; set; }
    public Guid TreinadorId { get; set; }

    public string Texto { get; set; } = null!;
    public DateTime EnviadaEm { get; set; }

    public LigaPartida? Partida { get; set; }
    public Mosaico? Mosaico { get; set; }
    public Treinador Treinador { get; set; } = null!;
}
