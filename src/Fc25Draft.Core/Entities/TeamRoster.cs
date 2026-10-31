namespace Fc25Draft.Core.Entities
{
    public class TeamRoster
    {
        public Guid TeamId { get; set; }
        public int PlayerId { get; set; }

        /// <summary>
        /// Preço pedido pelo dono quando coloca o jogador como negociável (lista de transferência).
        /// Nulo = não está à venda. Fica no vínculo com o time, então some sozinho quando ele muda de clube.
        /// </summary>
        public decimal? AskingPrice { get; set; }

        /// <summary>Quando o jogador entrou na lista de transferência.</summary>
        public DateTime? ListedAtUtc { get; set; }

        /// <summary>
        /// Quando o jogador pediu para sair (insatisfeito que seguiu no banco): foi sozinho para a lista pelo preço da
        /// venda rápida e o dono só tira com a promessa de colocar para jogar.
        /// </summary>
        public DateTime? SaidaPedidaEm { get; set; }

        /// <summary>Variação do valor travada enquanto ele está na lista por ter pedido para sair (o preço em baixa).</summary>
        public decimal? VariacaoCongelada { get; set; }

        /// <summary>Promessa de colocar para jogar em andamento desde (tirou da lista com ela).</summary>
        public DateTime? PromessaEm { get; set; }

        /// <summary>Já quebraram uma promessa com ele neste clube: voltou à lista pela metade e não aceita outra.</summary>
        public bool PromessaQuebrada { get; set; }

        /// <summary>
        /// Papel no elenco: o que ele espera do clube. Gravado logo que chega (pela chegada), na virada (pelo que jogou)
        /// e na conversa. Nulo = ainda não gravado (a conta deduz pela chegada).
        /// </summary>
        public Utilities.PapelNoElenco? Papel { get; set; }

        /// <summary>O clube rebaixou o papel dele numa conversa (o valor cai um pouco por alguns jogos).</summary>
        public DateTime? ConversaEm { get; set; }

        public Team Team { get; set; } = null!;
        public Player Player { get; set; } = null!;
    }
}
