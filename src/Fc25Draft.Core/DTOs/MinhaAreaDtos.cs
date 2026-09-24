using System;
using System.Collections.Generic;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.DTOs;

/// <summary>Leilão ativo em que o time deu lance.</summary>
public record MinhaAreaLeilaoDto(
    Guid ItemId, int PlayerId, string JogadorNome, string Posicao, int Overall,
    decimal LanceAtual, string? LiderNome, bool EstouGanhando, decimal MeuMaiorLance, DateTime TerminaEm);

/// <summary>Proposta de troca/venda ainda pendente, recebida ou enviada.</summary>
public record MinhaAreaPropostaDto(
    Guid OfferId, bool Recebida, string OutroTimeNome, OfferType Tipo, decimal Dinheiro,
    IReadOnlyList<string> Jogadores, DateTime CriadaEm);

public record MinhaAreaDraftDto(
    string DraftNome,
    // Nulo quando o time não tem mais escolhas neste draft.
    int? ProximaRodada,
    int? ProximaEscolhaGeral,
    // Quantas escolhas faltam até a vez do time (0 = é a vez dele).
    int? EscolhasAteMinhaVez,
    int EscolhasRestantes);

public record MinhaAreaAutoPickDto(bool ProximoDraft, bool Configurada, bool Ativa, DraftAutoPickModo Modo, int Jogadores);

/// <summary>Jogador observado e onde ele está agora.</summary>
public record MinhaAreaObservadoDto(
    int PlayerId, string Nome, string Posicao, int Overall, int? Idade,
    string? TimeNome,
    // Preenchidos quando o jogador está num leilão ativo.
    decimal? LeilaoLanceAtual, string? LeilaoLiderNome, DateTime? LeilaoTerminaEm, bool LeilaoEuLidero);

public record MinhaAreaDto(
    Guid TeamId,
    string TeamName,
    string? OwnerName,
    decimal Caixa,
    decimal CaixaBloqueado,
    int Elenco,
    IReadOnlyList<MinhaAreaLeilaoDto> Leiloes,
    IReadOnlyList<MinhaAreaPropostaDto> Propostas,
    MinhaAreaDraftDto? Draft,
    MinhaAreaAutoPickDto? EscolhaAutomatica,
    IReadOnlyList<MinhaAreaObservadoDto> Observados)
{
    public decimal CaixaLivre => Caixa - CaixaBloqueado;
}
