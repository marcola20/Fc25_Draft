using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>O valor de mercado do jogador jogo a jogo no clube atual, para o gráfico da ficha.</summary>
public interface IValorDoJogadorService
{
    /// <summary>Nulo se ele não tem clube, não tem idade cadastrada ou ainda não teve jogo pelo clube.</summary>
    Task<ValorDoJogadorDto?> HistoricoAsync(int playerId, CancellationToken ct);
}
