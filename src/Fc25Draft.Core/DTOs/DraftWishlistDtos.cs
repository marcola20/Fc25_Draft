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

public record DraftWishlistDto(
    Guid TeamId,
    string TeamName,
    DateTime? EnviadoEm,
    IReadOnlyList<DraftWishlistPlayerDto> Jogadores);

public record DraftWishlistSaveRequestDto(IReadOnlyList<int> PlayerIds);

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
