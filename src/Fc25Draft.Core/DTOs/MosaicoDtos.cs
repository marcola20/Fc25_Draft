using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.DTOs;

/// <summary>Uma rodada que passa no mosaico. <paramref name="Rotulo"/>: "Série A · Rodada 38".</summary>
public record MosaicoRodadaDto(Guid RodadaId, Guid LigaId, string Competicao, int Numero, string Rotulo);

public record MosaicoDto(
    Guid MosaicoId,
    string Titulo,
    string? YoutubeVideoId,
    DateTime CriadoEm,
    IReadOnlyList<MosaicoRodadaDto> Rodadas);

/// <summary>Um jogo do mosaico, para o telão listar os palpites.</summary>
public record MosaicoJogoDto(
    Guid PartidaId,
    string Competicao,
    string TimeCasaNome,
    string TimeForaNome,
    PartidaStatus Status,
    int GolsCasa,
    int GolsFora,
    string? PenaltisVencedorNome);

/// <summary>O que o telão do mosaico precisa: o vídeo, as rodadas e os jogos delas.</summary>
public record TelaoMosaicoDto(
    Guid MosaicoId,
    string Titulo,
    string? YoutubeVideoId,
    DateTime CriadoEm,
    IReadOnlyList<MosaicoRodadaDto> Rodadas,
    IReadOnlyList<MosaicoJogoDto> Jogos);

/// <param name="Link">Link do YouTube; vazio deixa o mosaico sem vídeo (fica fora da Central).</param>
public record MosaicoSalvarRequest(string Titulo, string? Link, IReadOnlyList<Guid> RodadaIds);

/// <summary>Uma competição da temporada, com as rodadas dela, para escolher o que entra no mosaico.</summary>
/// <param name="Sugerida">A rodada que já vem marcada: a última da liga.</param>
public record MosaicoCompeticaoOpcaoDto(
    Guid LigaId,
    string Competicao,
    int? Temporada,
    IReadOnlyList<MosaicoRodadaOpcaoDto> Rodadas,
    Guid? Sugerida);

public record MosaicoRodadaOpcaoDto(Guid RodadaId, int Numero, DateTime? Quando);
