using System;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.DTOs;

public record QuickSellResultDto(
    Guid TeamId,
    string TeamName,
    int PlayerId,
    Guid PlayerGuid,
    string PlayerName,
    int OldOverall,
    int NewOverall,
    PlayerStatus Status,
    decimal BasePrice,
    decimal Payout,
    decimal BudgetAfter,
    DateTime OccurredAtUtc);
