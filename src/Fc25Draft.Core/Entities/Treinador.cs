namespace Fc25Draft.Core.Entities;

/// <summary>Por que a passagem acabou.</summary>
public enum MotivoSaidaTreinador
{
    PediuDemissao = 1,
    Demitido = 2,
    Outro = 3
}

/// <summary>Papel da pessoa no time.</summary>
public enum PapelTreinador
{
    Treinador = 1,
    Auxiliar = 2
}

/// <summary>
/// A pessoa por trás do time. O token é dela, não do clube: assim o treinador leva o acesso
/// quando troca de time e o auxiliar entra com o dele.
/// </summary>
public class Treinador
{
    public Guid TreinadorId { get; set; }

    public string Nome { get; set; } = null!;

    /// <summary>Token pessoal de acesso.</summary>
    public string Token { get; set; } = null!;

    /// <summary>Fora da liga não aparece para escolher, mas a carreira continua.</summary>
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }

    public ICollection<TreinadorPassagem> Passagens { get; set; } = new List<TreinadorPassagem>();
}

/// <summary>
/// Passagem de um treinador por um clube: de quando entrou até quando saiu (nulo = está lá).
/// É daqui que sai a carreira dele.
/// </summary>
public class TreinadorPassagem
{
    public Guid PassagemId { get; set; }
    public Guid TreinadorId { get; set; }
    public Guid TimeId { get; set; }

    public PapelTreinador Papel { get; set; }

    public DateTime Desde { get; set; }

    /// <summary>Nulo enquanto a passagem está valendo.</summary>
    public DateTime? Ate { get; set; }

    /// <summary>Pediu demissão, foi demitido ou outro (nulo nas passagens antigas e nas abertas).</summary>
    public MotivoSaidaTreinador? MotivoSaida { get; set; }

    /// <summary>
    /// Momento (UTC) em que a saída foi registrada. Na diretoria, é daqui que o técnico novo começa do zero
    /// (os jogos até ali ainda são do anterior).
    /// </summary>
    public DateTime? SaiuEm { get; set; }

    public Treinador Treinador { get; set; } = null!;
    public Team Time { get; set; } = null!;
}
