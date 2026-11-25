using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>O próximo clássico marcado entre os dois, com o lado de A.</summary>
public record ProximoClassicoDto(
    Guid PartidaId,
    Guid LigaId,
    string Competicao,
    string CasaNome,
    string ForaNome,
    DateTime? Quando,
    bool ADaCasa);

/// <summary>
/// Uma rivalidade com o retrospecto só dos clássicos (os dois de técnico principal), contado do lado de
/// <see cref="A"/>; jogos do mais recente para o mais antigo. Prêmios: quanto cada um já levou em clássicos.
/// </summary>
public record RivalidadeDto(
    Guid RivalidadeId,
    string? Nome,
    PerfilTreinadorDto A,
    PerfilTreinadorDto B,
    string? TimeA,
    string? TimeB,
    TreinadorRetrospectoDto Retrospecto,
    IReadOnlyList<TreinadorConfrontoJogoDto> Jogos,
    ProximoClassicoDto? Proximo,
    decimal PremiosA,
    decimal PremiosB,
    DateTime CriadaEm)
{
    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? $"{A.NomeCurto} x {B.NomeCurto}" : Nome!;
}

/// <summary>O selo de clássico num jogo.</summary>
public record ClassicoDaPartidaDto(Guid PartidaId, Guid RivalidadeId, string Titulo);

/// <summary>Clássicos entre treinadores rivais: cadastro, retrospecto, selo nos jogos e prêmio ao vencedor.</summary>
public interface IClassicoService
{
    Task<IReadOnlyList<RivalidadeDto>> ListarAsync(CancellationToken ct);

    /// <summary>Dos jogos pedidos, os que são clássicos (agendados: com os técnicos de hoje).</summary>
    Task<IReadOnlyDictionary<Guid, ClassicoDaPartidaDto>> DasPartidasAsync(IReadOnlyCollection<Guid> partidaIds, CancellationToken ct);

    /// <summary>Cria (sem id) ou renomeia uma rivalidade.</summary>
    Task SalvarAsync(Guid? rivalidadeId, Guid treinadorA, Guid treinadorB, string? nome, CancellationToken ct);

    Task RemoverAsync(Guid rivalidadeId, CancellationToken ct);

    Task<decimal> PremioAsync(CancellationToken ct);

    Task SalvarPremioAsync(decimal valor, CancellationToken ct);

    /// <summary>Paga os clássicos encerrados ainda não pagos. Retorna quantos pagou.</summary>
    Task<int> PagarPremiosAsync(CancellationToken ct);
}
