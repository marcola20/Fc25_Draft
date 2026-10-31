using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Entities;

/// <summary>
/// Um jogador escolhido para o treino da semana (até 3 por time). Quando a semana fecha, o treino acontece: o
/// atributo sobe (com a <see cref="EvolucaoPes"/> pendente para o Editor PES) ou fica o motivo de não ter subido.
/// </summary>
public class TreinoSemanal
{
    public Guid TreinoId { get; set; }
    public Guid TeamId { get; set; }
    public int PlayerId { get; set; }

    /// <summary>Segunda-feira da semana (data de Brasília, sem hora).</summary>
    public DateTime Semana { get; set; }

    public FocoDoTreino Foco { get; set; }
    public DateTime EscolhidoEm { get; set; }

    /// <summary>Quando o treino aconteceu (a semana fechou); nulo = ainda vai acontecer.</summary>
    public DateTime? AplicadoEm { get; set; }

    /// <summary>Atributo que subiu (nome do PES); nulo se não subiu.</summary>
    public string? Atributo { get; set; }

    /// <summary>Atributo que subiu a mais por ele ter padrinho no clube; nulo se não teve.</summary>
    public string? AtributoDoPadrinho { get; set; }

    public int? OverallAntes { get; set; }
    public int? OverallDepois { get; set; }

    /// <summary>Por que não subiu (saiu do clube, atributos no máximo); nulo se subiu ou ainda não aconteceu.</summary>
    public string? SemEfeito { get; set; }

    public int? EvolucaoPesId { get; set; }

    public Team Team { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
