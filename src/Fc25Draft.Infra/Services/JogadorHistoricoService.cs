using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>Jogo a jogo (com a nota do PES) e evolução do overall de um jogador.</summary>
public class JogadorHistoricoService : IJogadorHistoricoService
{
    private readonly DraftDbContext _db;

    public JogadorHistoricoService(DraftDbContext db) => _db = db;

    public async Task<JogadorJogosDto> JogosAsync(int playerId, CancellationToken ct)
    {
        // Onde o jogador aparece: escalação, nota do PES ou lance (gol, assistência, cartão, substituição).
        var escalacoes = await _db.LigaEscalacoes.AsNoTracking()
            .Where(e => e.JogadorId == playerId)
            .Select(e => new { e.PartidaId, e.TimeId, e.Titular })
            .ToListAsync(ct);
        var notas = await _db.LigaNotasJogadores.AsNoTracking()
            .Where(n => n.JogadorId == playerId)
            .Select(n => new { n.PartidaId, n.TimeId, n.Nota, n.MelhorEmCampo })
            .ToListAsync(ct);
        var eventos = await _db.LigaEventos.AsNoTracking()
            .Where(e => e.JogadorId == playerId || e.AssistenteId == playerId)
            .Select(e => new { e.PartidaId, e.Tipo, e.TimeId, e.JogadorId, e.AssistenteId })
            .ToListAsync(ct);

        var ids = escalacoes.Select(e => e.PartidaId)
            .Concat(notas.Select(n => n.PartidaId))
            .Concat(eventos.Select(e => e.PartidaId))
            .Distinct()
            .ToList();
        if (ids.Count == 0) return new JogadorJogosDto(Array.Empty<JogadorJogoDto>(), 0, null, null, 0);

        var partidas = await _db.LigaPartidas.AsNoTracking()
            .Where(p => ids.Contains(p.PartidaId) && p.Status != PartidaStatus.Agendada)
            .Select(p => new
            {
                p.PartidaId,
                p.TimeCasaId,
                Casa = p.TimeCasa.TeamName,
                p.TimeForaId,
                Fora = p.TimeFora.TeamName,
                p.GolsCasa,
                p.GolsFora,
                p.Status,
                p.EncerradaEm,
                p.Rodada.Numero,
                p.Rodada.Desempate,
                p.Rodada.DataHora,
                p.Rodada.Liga.Tipo,
                p.Rodada.Liga.Divisao,
                p.Rodada.Liga.Temporada
            })
            .ToListAsync(ct);

        var jogos = new List<JogadorJogoDto>();
        foreach (var p in partidas)
        {
            var escalacao = escalacoes.FirstOrDefault(e => e.PartidaId == p.PartidaId);
            var nota = notas.FirstOrDefault(n => n.PartidaId == p.PartidaId);
            var lances = eventos.Where(e => e.PartidaId == p.PartidaId).ToList();
            var proprios = lances.Where(e => e.JogadorId == playerId).ToList();

            // Reserva que não entrou não jogou.
            var entrou = proprios.Any(e => e.Tipo == TipoEvento.Substituicao);
            var titular = escalacao?.Titular == true;
            var participou = titular || entrou || nota is not null || lances.Count > 0;
            if (!participou) continue;

            var timeId = escalacao?.TimeId
                         ?? nota?.TimeId
                         ?? proprios.FirstOrDefault(e => e.Tipo != TipoEvento.GolContra)?.TimeId
                         ?? lances.First().TimeId;
            if (timeId != p.TimeCasaId && timeId != p.TimeForaId) continue;

            var emCasa = timeId == p.TimeCasaId;
            var golsPro = emCasa ? p.GolsCasa : p.GolsFora;
            var golsContra = emCasa ? p.GolsFora : p.GolsCasa;
            var resultado = p.Status != PartidaStatus.Encerrada ? null
                : golsPro > golsContra ? "V" : golsPro < golsContra ? "D" : "E";

            var competicao = LigaLabels.Competicao(p.Tipo, p.Divisao) + (p.Temporada is int t ? $" {t}" : "");
            DateTime? quando = p.DataHora
                ?? (p.EncerradaEm is DateTime fim ? TimeZoneInfo.ConvertTimeFromUtc(Utc(fim), HorarioDeBrasilia.Fuso) : null);

            jogos.Add(new JogadorJogoDto(
                p.PartidaId, quando, competicao, Rotulo(p.Numero, p.Desempate),
                timeId, emCasa ? p.Casa : p.Fora,
                emCasa ? p.TimeForaId : p.TimeCasaId, emCasa ? p.Fora : p.Casa,
                emCasa, golsPro, golsContra, resultado,
                escalacao is not null ? escalacao.Titular : entrou ? false : null,
                nota?.Nota, nota?.MelhorEmCampo == true,
                proprios.Count(e => e.Tipo == TipoEvento.Gol),
                lances.Count(e => e.Tipo == TipoEvento.Gol && e.AssistenteId == playerId),
                proprios.Count(e => e.Tipo == TipoEvento.CartaoAmarelo),
                proprios.Count(e => e.Tipo == TipoEvento.CartaoVermelho)));
        }

        jogos = jogos.OrderByDescending(j => j.Quando ?? DateTime.MinValue).ToList();
        var comNota = jogos.Where(j => j.Nota is not null).Select(j => j.Nota!.Value).ToList();

        return new JogadorJogosDto(
            jogos,
            comNota.Count,
            comNota.Count > 0 ? Math.Round(comNota.Average(), 2) : null,
            comNota.Count > 0 ? comNota.Max() : null,
            jogos.Count(j => j.MelhorEmCampo));
    }

    public async Task<IReadOnlyList<JogadorOverallPontoDto>> EvolucaoOverallAsync(int playerId, CancellationToken ct)
    {
        var transferencias = await _db.TransferHistories.AsNoTracking()
            .Where(t => t.PlayerId == playerId && t.OldOverall != null && t.NewOverall != null && t.OldOverall != t.NewOverall)
            .Select(t => new { Data = t.PerformedAtUtc, Antes = t.OldOverall!.Value, Depois = t.NewOverall!.Value, t.Type })
            .ToListAsync(ct);
        var evolucoes = await _db.EvolucoesPes.AsNoTracking()
            .Where(e => e.PlayerId == playerId && e.OverallAntes != e.OverallDepois)
            .Select(e => new { Data = e.CriadaEmUtc, Antes = e.OverallAntes, Depois = e.OverallDepois, e.Motivo })
            .ToListAsync(ct);

        var mudancas = transferencias
            .Select(t => (Data: Utc(t.Data), t.Antes, t.Depois, Motivo: t.Type == TransferType.QuickSell ? "Venda rápida" : "Transferência"))
            .Concat(evolucoes.Select(e => (Data: Utc(e.Data), e.Antes, e.Depois, e.Motivo)))
            .OrderBy(m => m.Data)
            .ToList();
        if (mudancas.Count == 0) return Array.Empty<JogadorOverallPontoDto>();

        // Começa no overall de antes da primeira mudança; cada mudança é um degrau.
        var pontos = new List<JogadorOverallPontoDto> { new(mudancas[0].Data, mudancas[0].Antes, "Início") };
        pontos.AddRange(mudancas.Select(m => new JogadorOverallPontoDto(m.Data, m.Depois, m.Motivo)));
        return pontos;
    }

    private static string Rotulo(int numero, bool desempate) => numero switch
    {
        0 => "Mata-mata",
        -1 => "Decisão do título",
        -2 => "Playoff de acesso",
        _ when desempate => "Jogo decisivo",
        _ => $"Rodada {numero}"
    };

    private static DateTime Utc(DateTime d) => d.Kind switch
    {
        DateTimeKind.Local => d.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(d, DateTimeKind.Utc),
        _ => d
    };
}
