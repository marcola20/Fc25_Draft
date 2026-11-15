using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Os mosaicos (vídeo com os jogos da rodada juntos) e o telão deles.</summary>
public interface IMosaicoService
{
    /// <summary>Todos, do mais novo para o mais antigo.</summary>
    Task<IReadOnlyList<MosaicoDto>> ListarAsync(CancellationToken ct);

    /// <summary>As ligas da temporada mais recente com as rodadas delas, para montar um mosaico.</summary>
    Task<IReadOnlyList<MosaicoCompeticaoOpcaoDto>> OpcoesAsync(CancellationToken ct);

    Task<MosaicoDto> CriarAsync(MosaicoSalvarRequest request, CancellationToken ct);

    Task<MosaicoDto> AtualizarAsync(Guid mosaicoId, MosaicoSalvarRequest request, CancellationToken ct);

    /// <summary>Apaga o mosaico junto com o chat e as reações dele.</summary>
    Task ApagarAsync(Guid mosaicoId, CancellationToken ct);

    Task<TelaoMosaicoDto?> TelaoAsync(Guid mosaicoId, CancellationToken ct);

    /// <summary>
    /// O mosaico que aparece na Central: o mais novo com vídeo, enquanto tiver menos de
    /// <see cref="DiasNaCentral"/> dias. Null quando não tem.
    /// </summary>
    Task<MosaicoDto?> NaCentralAsync(CancellationToken ct);

    /// <summary>Quantos dias o mosaico fica em destaque na Central.</summary>
    const int DiasNaCentral = 10;
}
