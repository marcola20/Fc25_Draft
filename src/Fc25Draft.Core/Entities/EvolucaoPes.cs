namespace Fc25Draft.Core.Entities;

/// <summary>
/// Mudança de atributos feita no site (ex.: venda rápida) que ainda precisa ir para o save do PES.
/// O Editor PES baixa as pendentes, soma as mudanças nos atributos do jogo, grava o EDIT e confirma.
/// </summary>
public class EvolucaoPes
{
    public int EvolucaoPesId { get; set; }
    public int PlayerId { get; set; }

    /// <summary>De onde veio: "Venda rápida"...</summary>
    public string Motivo { get; set; } = null!;

    public DateTime CriadaEmUtc { get; set; }
    public int OverallAntes { get; set; }
    public int OverallDepois { get; set; }

    /// <summary>Quanto cada atributo mudou: 25 números separados por vírgula, na ordem de AtributosPes.Todos.</summary>
    public string Mudancas { get; set; } = null!;

    /// <summary>Quando o Editor PES confirmou que gravou no save (nulo = pendente).</summary>
    public DateTime? AplicadaNoJogoEmUtc { get; set; }

    public Player Player { get; set; } = null!;

    public int[] LerMudancas() => Mudancas.Split(',').Select(int.Parse).ToArray();

    public static string EscreverMudancas(IReadOnlyList<int> antes, IReadOnlyList<int> depois) =>
        string.Join(",", antes.Select((v, i) => depois[i] - v));
}
