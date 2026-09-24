namespace Fc25Draft.Core.Exceptions;

public enum ResultadoPesErro
{
    /// <summary>JSON incompleto, marcado para revisão ou placar que não bate com os gols.</summary>
    Invalido,
    /// <summary>Nenhuma partida aberta com esse mandante x visitante.</summary>
    NaoEncontrado,
    /// <summary>Mais de uma partida possível, ou a rodada informada não confere.</summary>
    Ambiguo
}

/// <summary>Importação de resultado do PES recusada — nada foi gravado.</summary>
public class ResultadoPesException : Exception
{
    public ResultadoPesErro Erro { get; }
    public IReadOnlyList<string> Candidatos { get; }

    public ResultadoPesException(ResultadoPesErro erro, string message, IReadOnlyList<string>? candidatos = null)
        : base(message)
    {
        Erro = erro;
        Candidatos = candidatos ?? Array.Empty<string>();
    }
}
