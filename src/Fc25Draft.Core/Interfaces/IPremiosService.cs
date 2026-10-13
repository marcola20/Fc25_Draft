using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>
/// Prêmios de destaque da temporada (MVP, ROY, MIP, DPOY, GKOY, COTY): o admin monta os finalistas (o site sugere
/// pelos números da temporada), cada pessoa da liga vota, o admin confirma os vencedores e dá o +1 over pelos
/// atributos — a mudança vai para o save pelo Editor PES, como a venda rápida.
/// </summary>
public interface IPremiosService
{
    /// <summary>As temporadas que têm votação, da mais nova para a mais velha.</summary>
    Task<IReadOnlyList<int>> TemporadasAsync(CancellationToken ct);

    /// <summary>
    /// A votação da temporada. Os votos de cada candidato só aparecem com a votação encerrada, ou com
    /// <paramref name="comVotos"/> (admin).
    /// </summary>
    Task<VotacaoPremiosDto?> GetAsync(int temporada, bool comVotos, CancellationToken ct);

    /// <summary>A votação mais nova que o pessoal já pode ver (aberta, encerrada ou publicada).</summary>
    Task<VotacaoPremiosDto?> GetVisivelAsync(CancellationToken ct);

    // ── Admin: montar ────────────────────────────────────────────────────────

    /// <summary>Cria a votação da temporada com os finalistas sugeridos pelos números.</summary>
    Task CriarAsync(int temporada, CancellationToken ct);

    /// <summary>Joga fora os finalistas da categoria e põe as sugestões de novo (só antes de abrir).</summary>
    Task SugerirDeNovoAsync(int temporada, CategoriaPremio categoria, CancellationToken ct);

    Task AdicionarJogadorAsync(int temporada, CategoriaPremio categoria, int playerId, CancellationToken ct);
    Task AdicionarTreinadorAsync(int temporada, Guid treinadorId, CancellationToken ct);
    Task RemoverCandidatoAsync(Guid candidatoId, CancellationToken ct);

    Task<IReadOnlyList<PremioJogadorOpcaoDto>> BuscarJogadoresAsync(string termo, CancellationToken ct);
    Task<IReadOnlyList<PremioTreinadorOpcaoDto>> TreinadoresAsync(CancellationToken ct);

    // ── Admin: andamento ─────────────────────────────────────────────────────

    Task AbrirAsync(int temporada, CancellationToken ct);
    Task EncerrarAsync(int temporada, CancellationToken ct);

    /// <summary>Encerrada → aberta de novo (alguém pediu mais um tempo).</summary>
    Task ReabrirAsync(int temporada, CancellationToken ct);

    /// <summary>Volta para a montagem (sem votos ainda). Com voto não volta.</summary>
    Task VoltarParaMontagemAsync(int temporada, CancellationToken ct);

    Task<IReadOnlyList<PremioEleitorDto>> EleitoresAsync(int temporada, CancellationToken ct);
    Task<IReadOnlyList<PremioVotosDoEleitorDto>> CedulasAsync(int temporada, CancellationToken ct);

    Task MarcarVencedorAsync(Guid candidatoId, bool vencedor, CancellationToken ct);
    Task PublicarAsync(int temporada, CancellationToken ct);

    /// <summary>
    /// +1 over do prêmio: sobe os atributos até a fórmula dar +1 e deixa a mudança pendente para o Editor PES.
    /// <paramref name="paraPlayerId"/> é o companheiro que recebe no lugar (quem já levou over por outro prêmio).
    /// </summary>
    Task<string> AplicarOverAsync(Guid candidatoId, int? paraPlayerId, CancellationToken ct);

    /// <summary>Desfaz o +1 over enquanto o Editor PES ainda não levou a mudança para o save.</summary>
    Task<string> DesfazerOverAsync(Guid candidatoId, CancellationToken ct);

    /// <summary>Companheiros de clube do vencedor, para repassar o over.</summary>
    Task<IReadOnlyList<PremioJogadorOpcaoDto>> CompanheirosAsync(Guid candidatoId, CancellationToken ct);

    // ── Pessoal ──────────────────────────────────────────────────────────────

    Task<PremioCedulaDto> CedulaAsync(int temporada, Guid eleitorId, CancellationToken ct);

    /// <summary>Grava a cédula inteira (troca a anterior). Só com a votação aberta.</summary>
    Task VotarAsync(int temporada, Guid eleitorId, IReadOnlyCollection<Guid> candidatos, CancellationToken ct);

    Task<IReadOnlyList<PremioConquistadoDto>> DoJogadorAsync(int playerId, CancellationToken ct);
    Task<IReadOnlyList<PremioConquistadoDto>> DoTreinadorAsync(Guid treinadorId, CancellationToken ct);

    Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct);
}
