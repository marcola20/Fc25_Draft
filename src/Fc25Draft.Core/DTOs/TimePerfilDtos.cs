using System;
using System.Collections.Generic;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.DTOs;

/// <summary>Um jogo visto pelo lado do time. Placar e resultado ficam nulos enquanto não foi jogado.</summary>
public record TimePerfilJogoDto(
    Guid PartidaId,
    string Competicao,
    string Etapa,
    Guid AdversarioId,
    string AdversarioNome,
    bool EmCasa,
    int? GolsPro,
    int? GolsContra,
    // "V", "E" ou "D". Decisão por pênaltis conta como empate (placar do tempo normal).
    string? Resultado,
    // Só em jogo com pênaltis: se o time venceu a disputa.
    bool? VenceuPenaltis,
    bool IsWO,
    DateTime? Data);

/// <summary>Retrospecto contra um adversário em todas as competições.</summary>
public record TimeConfrontoDto(
    Guid AdversarioId,
    string AdversarioNome,
    int Jogos,
    int Vitorias,
    int Empates,
    int Derrotas,
    int GolsPro,
    int GolsContra)
{
    public int SaldoGols => GolsPro - GolsContra;
}

public record TimePerfilDto(
    Guid TimeId,
    int Jogos,
    int Vitorias,
    int Empates,
    int Derrotas,
    int GolsPro,
    int GolsContra,
    // Últimos 5 jogos, do mais recente para o mais antigo.
    IReadOnlyList<TimePerfilJogoDto> Forma,
    // Ex.: "4 vitórias seguidas", "Invicto há 6 jogos". Nulo sem jogos.
    string? SequenciaAtual,
    // "V", "E" ou "D": dá a cor da sequência.
    string? SequenciaTipo,
    TimePerfilJogoDto? MaiorVitoria,
    TimePerfilJogoDto? MaiorDerrota,
    int MaiorSequenciaVitorias,
    int MaiorInvencibilidade,
    IReadOnlyList<TimePerfilJogoDto> UltimosJogos,
    IReadOnlyList<TimePerfilJogoDto> ProximosJogos,
    IReadOnlyList<TimeConfrontoDto> Confrontos)
{
    public int SaldoGols => GolsPro - GolsContra;
    public TimePerfilJogoDto? ProximoJogo => ProximosJogos.Count > 0 ? ProximosJogos[0] : null;
}

public record TimeTransferenciaDto(
    DateTime Data,
    TransferType Tipo,
    int PlayerId,
    string JogadorNome,
    // true = chegou ao time; false = saiu.
    bool Entrada,
    string? OutroTimeNome,
    decimal? Valor);

public record TimeTransferenciasDto(
    int Chegadas,
    int Saidas,
    // Pago em leilões e compras de outros times.
    decimal Gasto,
    // Recebido em vendas, vendas rápidas e compensações do draft de expansão.
    decimal Recebido,
    IReadOnlyList<TimeTransferenciaDto> Movimentacoes)
{
    public decimal Saldo => Recebido - Gasto;
}
