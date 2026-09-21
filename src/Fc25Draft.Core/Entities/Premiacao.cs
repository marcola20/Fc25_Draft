using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

/// <summary>
/// Tabela de premiação de uma temporada. Cada temporada teve valores diferentes,
/// então a premiação é cadastrada (normalmente copiando a da temporada anterior)
/// em vez de ficar fixa na página.
/// </summary>
public class Premiacao
{
    public Guid PremiacaoId { get; set; }

    /// <summary>Ano da temporada (2009, 2010...), igual ao da Liga.</summary>
    public int Temporada { get; set; }

    public string Nome { get; set; } = null!;

    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }

    public ICollection<PremiacaoItem> Itens { get; set; } = new List<PremiacaoItem>();
}

/// <summary>Um prêmio: por posição (Liga) ou por fase alcançada (Copa e Supercopa).</summary>
public class PremiacaoItem
{
    public Guid PremiacaoItemId { get; set; }
    public Guid PremiacaoId { get; set; }

    public TipoCompetition Tipo { get; set; }

    /// <summary>Divisão da Liga premiada; nulo para Copa/Supercopa.</summary>
    public Divisao? Divisao { get; set; }

    /// <summary>Liga: primeira posição da faixa premiada (1 = campeão). Nulo em Copa/Supercopa.</summary>
    public int? PosicaoDe { get; set; }

    /// <summary>Liga: última posição da faixa premiada.</summary>
    public int? PosicaoAte { get; set; }

    /// <summary>Copa/Supercopa: fase alcançada. Nulo na Liga.</summary>
    public FasePremiacao? Fase { get; set; }

    public decimal Valor { get; set; }

    public Premiacao Premiacao { get; set; } = null!;
}

/// <summary>Premiação já creditada no caixa de um time por uma competição, para não pagar duas vezes.</summary>
public class PremiacaoPagamento
{
    public Guid PagamentoId { get; set; }
    public Guid PremiacaoId { get; set; }
    public Guid LigaId { get; set; }
    public Guid TimeId { get; set; }

    public decimal Valor { get; set; }

    /// <summary>Por que recebeu: "3º lugar", "Campeão da Copa"...</summary>
    public string Motivo { get; set; } = null!;

    public DateTime PagoEm { get; set; }

    public Premiacao Premiacao { get; set; } = null!;
    public Liga Liga { get; set; } = null!;
    public Team Time { get; set; } = null!;
}
