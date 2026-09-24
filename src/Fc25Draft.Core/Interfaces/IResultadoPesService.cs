using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Importa o resultado de uma partida simulada no PES 2021 (JSON do Auto_PES21).</summary>
public interface IResultadoPesService
{
    /// <summary>
    /// Acha a partida pelo mandante x visitante, grava placar e eventos (substituindo os que
    /// havia) e recalcula a classificação. Com <paramref name="simular"/> faz tudo e desfaz.
    /// Recusa com <see cref="Exceptions.ResultadoPesException"/> sem gravar nada.
    /// </summary>
    Task<ResultadoPesRespostaDto> ImportarAsync(ResultadoPesRequest request, string jsonBruto, bool simular, CancellationToken ct);
}
