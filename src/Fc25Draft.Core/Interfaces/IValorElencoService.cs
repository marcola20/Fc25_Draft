namespace Fc25Draft.Core.Interfaces;

/// <summary>O elenco do time num momento: quantos jogadores, quanto valem (a preços de hoje) e o overall médio.</summary>
public record ValorElencoPontoDto(string Rotulo, DateTime Data, decimal Valor, int Jogadores, decimal OverallMedio);

/// <summary>Valor do elenco ao longo do tempo (gráfico na página do time).</summary>
public interface IValorElencoService
{
    /// <summary>
    /// Um ponto no começo do mercado, um ao fim de cada janela e um hoje. O elenco de cada momento é refeito
    /// desfazendo as transferências a partir do elenco atual; os valores são os de mercado de hoje, para comparar
    /// os elencos entre si.
    /// </summary>
    Task<IReadOnlyList<ValorElencoPontoDto>> HistoricoAsync(Guid timeId, CancellationToken ct);
}
