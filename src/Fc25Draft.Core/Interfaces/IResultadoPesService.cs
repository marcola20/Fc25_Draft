using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Importa o resultado de uma partida simulada no PES 2021 (JSON do Auto_PES21).</summary>
public interface IResultadoPesService
{
    /// <summary>
    /// Acha a partida pelo mandante x visitante, grava placar e eventos (substituindo os que
    /// havia) e recalcula a classificação. Com <paramref name="simular"/> faz tudo e desfaz.
    /// <paramref name="partidaAnterior"/> = partida que recebeu o envio anterior deste JSON; desfaz
    /// a dúvida quando o par se repete em outra competição. Recusa com
    /// <see cref="Exceptions.ResultadoPesException"/> sem gravar nada.
    /// </summary>
    Task<ResultadoPesRespostaDto> ImportarAsync(ResultadoPesRequest request, string jsonBruto, Guid? partidaAnterior, bool simular, CancellationToken ct);

    /// <summary>
    /// Tira as notas dos JSONs já importados e grava por jogador. Com <paramref name="somenteSemNotas"/>,
    /// só as partidas que ainda não têm nenhuma nota gravada. Retorna quantas notas foram gravadas.
    /// </summary>
    Task<int> ReprocessarNotasAsync(bool somenteSemNotas, CancellationToken ct);
}
