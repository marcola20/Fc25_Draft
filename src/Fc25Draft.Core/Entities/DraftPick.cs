namespace Fc25Draft.Core.Entities
{
    public class DraftPick
    {
        public Guid DraftId { get; set; }
        public int OverallPick { get; set; }

        public int RoundNumber { get; set; }
        public int PickInRound { get; set; }
        public Guid TeamId { get; set; }

        public int? PlayerId { get; set; }
        public DateTime? PickedAtUtc { get; set; }

        /// <summary>Draft de expansão: time que perdeu o jogador.</summary>
        public Guid? FromTeamId { get; set; }

        /// <summary>Draft de expansão: valor creditado ao time que perdeu o jogador.</summary>
        public decimal? Compensacao { get; set; }

        /// <summary>Feita pela escolha automática (lista de prioridade do time).</summary>
        public bool Automatica { get; set; }

        /// <summary>O tempo do time acabou e o site escolheu (pela lista ou pelo melhor overall).</summary>
        public bool TempoEsgotado { get; set; }

        public Draft Draft { get; set; } = null!;
        public DraftRound Round { get; set; } = null!;
        public Team Team { get; set; } = null!;
        public Team? FromTeam { get; set; }
        public Player? Player { get; set; }
    }
}
