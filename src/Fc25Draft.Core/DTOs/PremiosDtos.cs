using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.DTOs;

/// <summary>Um finalista. <see cref="Votos"/> só vem preenchido depois que a votação fecha (ou para o admin).</summary>
public record PremioCandidatoDto(
    Guid CandidatoId,
    CategoriaPremio Categoria,
    int? PlayerId,
    Guid? TreinadorId,
    string Nome,
    Guid? TimeId,
    string? TimeNome,
    string? Posicao,
    int? Overall,
    string? Resumo,
    int? Votos,
    bool Vencedor,
    int? OverParaPlayerId,
    string? OverParaNome,
    DateTime? OverAplicadoEm);

public record PremioCategoriaDto(PremiosTemporada.Regra Regra, IReadOnlyList<PremioCandidatoDto> Candidatos)
{
    public IReadOnlyList<PremioCandidatoDto> Vencedores => Candidatos.Where(c => c.Vencedor).ToList();
}

public record VotacaoPremiosDto(
    int Temporada,
    SituacaoVotacao Situacao,
    DateTime? AbertaEm,
    DateTime? EncerradaEm,
    DateTime? PublicadaEm,
    IReadOnlyList<PremioCategoriaDto> Categorias,
    int Eleitores,
    int Votaram);

/// <summary>O que a pessoa já marcou e o clube dela (os candidatos de lá ficam bloqueados).</summary>
public record PremioCedulaDto(Guid EleitorId, Guid? TimeId, IReadOnlyList<Guid> Votos);

/// <summary>Quem pode votar e se já votou (para o admin cobrar).</summary>
public record PremioEleitorDto(Guid TreinadorId, string Nome, string? TimeNome, bool Votou);

/// <summary>A cédula de cada pessoa. Só o admin vê.</summary>
public record PremioVotosDoEleitorDto(Guid EleitorId, string Nome, IReadOnlyList<Guid> Candidatos);

/// <summary>Jogador para o admin colocar como finalista.</summary>
public record PremioJogadorOpcaoDto(int PlayerId, string Nome, string Posicao, int Overall, string? TimeNome);

/// <summary>Treinador para o admin colocar como finalista do COTY.</summary>
public record PremioTreinadorOpcaoDto(Guid TreinadorId, string Nome, string? TimeNome);

/// <summary>Um prêmio ganho, para a ficha do jogador ou do treinador.</summary>
public record PremioConquistadoDto(int Temporada, CategoriaPremio Categoria, string? TimeNome)
{
    public string Titulo => PremiosTemporada.Titulo(Categoria, Temporada);
    public PremiosTemporada.Regra Regra => PremiosTemporada.De(Categoria);
}
