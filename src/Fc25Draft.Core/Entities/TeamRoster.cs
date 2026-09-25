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

        public Team Team { get; set; } = null!;
        public Player Player { get; set; } = null!;
    }
}
