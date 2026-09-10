using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs;

public record DraftWishlistPlayerDto(
    int Ordem,
    int PlayerId,
    string Name,
    short PositionId,
    string PositionName,
    int Overall,
    int? Age,
    bool Disponivel);

/// <summary>Uma versão das listas de pré-draft (rodada de envio).</summary>
public record DraftWishlistEdicaoDto(
    int Numero,
    string Nome,
    DateTime CriadoEm,
    DateTime? EncerradoEm,
    bool Aberta,
    int TotalListas);

public record DraftWishlistDto(
    int Versao,
    string VersaoNome,
    bool VersaoAberta,
    Guid TeamId,
    string TeamName,
    DateTime? EnviadoEm,
    IReadOnlyList<DraftWishlistPlayerDto> Jogadores);

public record DraftWishlistSaveRequestDto(IReadOnlyList<int> PlayerIds);

public record DraftWishlistNovaEdicaoRequestDto(string? Nome);

public record DraftWishlistEdicaoStatusRequestDto(bool Aberta);

/// <summary>Jogador que apareceu em pelo menos uma lista, com a contagem de votos (times que o listaram).</summary>
public record DraftWishlistVoteDto(
    int PlayerId,
    string Name,
    short PositionId,
    string PositionName,
    int Overall,
    int? Age,
    bool Disponivel,
    int Votos,
    int MelhorPosicao,
    IReadOnlyList<DraftWishlistVoteTeamDto> Times);

public record DraftWishlistVoteTeamDto(Guid TeamId, string TeamName, int Ordem);
