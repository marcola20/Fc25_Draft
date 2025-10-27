namespace Fc25Draft.Core.DTOs;

public record BudgetSummaryDto(Guid TeamId, decimal Saldo, decimal Bloqueado, decimal Disponivel);

public record BudgetAdjustRequestDto(Guid TeamId, string Tipo, decimal Valor, string Origem, string? Descricao);

public record MatchRewardRequestDto(Guid TeamId, int GolsFeitos, int GolsSofridos, bool CleanSheet, string Resultado);

public record LedgerItemDto(DateTime DataUtc, string Tipo, string Origem, decimal Valor, string? Descricao);

public record MatchRewardResult(Guid TeamId, decimal ValorAplicado, decimal SaldoAtual, bool AjusteRealizado, string Tipo, string Descricao);
