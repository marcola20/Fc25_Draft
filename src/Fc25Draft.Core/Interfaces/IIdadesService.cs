using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>
/// Idade dos jogadores: o site manda. Uma vez por temporada todos fazem aniversário (+1); o Editor PES copia a
/// idade do site para o save quando abre.
/// </summary>
public interface IIdadesService
{
    Task<EnvelhecimentoSituacaoDto> GetSituacaoAsync(CancellationToken ct);

    /// <summary>
    /// Em que temporada estão as idades do site e quantos anos somar à idade do jogo (2008) para chegar nela.
    /// Usado pelo Editor PES ao colocar um jogador novo no site.
    /// </summary>
    Task<IdadeDaLigaDto> GetIdadeDaLigaAsync(CancellationToken ct);

    /// <summary>Todos os jogadores com idade ganham 1 ano. Recusa se a temporada já foi envelhecida.</summary>
    Task<EnvelhecimentoSituacaoDto> EnvelhecerAsync(int temporada, CancellationToken ct);

    /// <summary>Desfaz o envelhecimento da temporada (todos −1 ano), para corrigir um clique errado.</summary>
    Task<EnvelhecimentoSituacaoDto> DesfazerAsync(int temporada, CancellationToken ct);
}
