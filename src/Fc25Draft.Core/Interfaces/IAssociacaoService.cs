using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>
/// Associação de Treinadores: eleição da Diretoria Executiva por chapas, com voto secreto (a urna guarda só
/// a chapa escolhida; a lista de eleitores guarda só se a pessoa votou) e a diretoria em exercício.
/// </summary>
public interface IAssociacaoService
{
    // ── Página ───────────────────────────────────────────────────────────────

    /// <summary>A eleição mais nova que o pessoal já vê (fora do rascunho). A contagem só no resultado.</summary>
    Task<EleicaoDto?> GetAtualAsync(CancellationToken ct);

    /// <summary>A chapa eleita na última eleição com vencedor.</summary>
    Task<DiretoriaAssociacaoDto?> GetDiretoriaAsync(CancellationToken ct);

    Task<IReadOnlyList<EleicaoEleitorDto>> EleitoresAsync(Guid eleicaoId, CancellationToken ct);

    /// <summary>Voto único e definitivo. <paramref name="chapaId"/> nulo = em branco.</summary>
    Task VotarAsync(Guid eleicaoId, Guid treinadorId, Guid? chapaId, CancellationToken ct);

    Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct);

    // ── Admin ────────────────────────────────────────────────────────────────

    Task<IReadOnlyList<EleicaoResumoDto>> ListAsync(CancellationToken ct);

    /// <summary>Com <paramref name="comContagem"/> a contagem aparece já na apuração (admin).</summary>
    Task<EleicaoDto?> GetAsync(Guid eleicaoId, bool comContagem, CancellationToken ct);

    Task<EleicaoDto> CriarAsync(EleicaoSalvarRequest request, CancellationToken ct);
    Task<EleicaoDto> SalvarAsync(Guid eleicaoId, EleicaoSalvarRequest request, CancellationToken ct);

    /// <summary>Só sem voto.</summary>
    Task ExcluirAsync(Guid eleicaoId, CancellationToken ct);

    /// <summary>Cria (chapaId nulo) ou edita uma chapa. Só antes de abrir a urna.</summary>
    Task SalvarChapaAsync(Guid eleicaoId, Guid? chapaId, ChapaSalvarRequest request, CancellationToken ct);

    Task ExcluirChapaAsync(Guid chapaId, CancellationToken ct);

    /// <summary>Rascunho → inscrições: põe a eleição na página e monta a lista de eleitores.</summary>
    Task PublicarEditalAsync(Guid eleicaoId, CancellationToken ct);

    /// <summary>Monta de novo a lista de eleitores: quem está ativo e com clube hoje (treinador ou auxiliar).</summary>
    Task<int> RefazerEleitoresAsync(Guid eleicaoId, CancellationToken ct);

    Task AdicionarEleitorAsync(Guid eleicaoId, Guid treinadorId, CancellationToken ct);
    Task RemoverEleitorAsync(Guid eleicaoId, Guid treinadorId, CancellationToken ct);

    /// <summary>Inscrições → votação. Pede ao menos uma chapa e todas completas.</summary>
    Task AbrirVotacaoAsync(Guid eleicaoId, CancellationToken ct);

    Task EncerrarAsync(Guid eleicaoId, CancellationToken ct);

    /// <summary>Apuração → votação de novo (alguém pediu mais tempo).</summary>
    Task ReabrirAsync(Guid eleicaoId, CancellationToken ct);

    /// <summary>Volta um passo antes de ter voto (votação → inscrições, inscrições → rascunho).</summary>
    Task VoltarAsync(Guid eleicaoId, CancellationToken ct);

    /// <summary>Homologa e publica. Devolve o que aconteceu (chapa eleita ou segundo turno).</summary>
    Task<string> PublicarResultadoAsync(Guid eleicaoId, CancellationToken ct);

    /// <summary>
    /// Cria a etapa que o resultado pede (Art. 10): 2º turno com as duas classificadas ou votação de desempate só
    /// com as empatadas. Em rascunho, com os mesmos eleitores.
    /// </summary>
    Task<EleicaoDto> CriarProximaEtapaAsync(Guid eleicaoId, CancellationToken ct);

    /// <summary>
    /// O desempate empatou de novo (Art. 10, VI e VII): a Presidência escolhe entre as empatadas. No 1º turno já
    /// cria o 2º turno com as escolhidas; no 2º, a escolhida é a eleita. Devolve o que aconteceu.
    /// </summary>
    Task<string> DecidirPelaPresidenciaAsync(Guid eleicaoId, IReadOnlyCollection<Guid> chapas, CancellationToken ct);
}
