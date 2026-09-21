using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities
{
    public class Draft
    {
        public Guid DraftId { get; set; }
        public string Name { get; set; } = null!;
        public int TotalTeams { get; set; }
        public int TotalRounds { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public DraftTipo Tipo { get; set; } = DraftTipo.Normal;

        // ── Só no draft de expansão ──
        /// <summary>Quantos jogadores cada time existente pode proteger.</summary>
        public int? ProtegidosPorTime { get; set; }

        /// <summary>Máximo de jogadores que um time existente pode perder.</summary>
        public int? MaxPerdasPorTime { get; set; }

        /// <summary>Compensação ao time que perde o jogador = valor de mercado × fator.</summary>
        public decimal? FatorCompensacao { get; set; }

        /// <summary>Quando as listas de protegidos foram fechadas; as escolhas só começam depois disso.</summary>
        public DateTime? ProtecaoEncerradaEm { get; set; }

        public ICollection<DraftRound> Rounds { get; set; } = new List<DraftRound>();
        public ICollection<DraftPick> Picks { get; set; } = new List<DraftPick>();
        public ICollection<DraftProtecao> Protecoes { get; set; } = new List<DraftProtecao>();
    }
}
