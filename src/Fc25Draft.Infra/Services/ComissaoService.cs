using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Comissão técnica: o time contrata, com o caixa e no começo da temporada, profissionais que dão vantagem no treino
/// da semana (treinador da base, treinador de goleiros) e na evolução da virada (preparador físico).
/// </summary>
public class ComissaoService : IComissaoService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public ComissaoService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<ComissaoDoTimeDto> DoTimeAsync(Guid teamId, CancellationToken ct)
    {
        var agora = HorarioDeBrasilia.Agora(_time);
        var (temporada, inicio) = await TemporadaAtualAsync(_db, agora, ct);
        var vale = temporada >= ComissaoCriterios.ValeAPartirDaTemporada;
        DateTime? ate = inicio?.Date.AddDays(ComissaoCriterios.DiasParaContratar);
        var contratados = await CargosAsync(_db, teamId, temporada, ct);
        var time = await _db.Teams.AsNoTracking().Where(t => t.TeamId == teamId)
            .Select(t => new { t.Budget, t.BudgetBlocked }).FirstOrDefaultAsync(ct);

        return new ComissaoDoTimeDto(temporada, vale, ate, vale && agora < ate, (time?.Budget ?? 0) - (time?.BudgetBlocked ?? 0),
            ComissaoDoClube.Cargos.Select(c => new ComissaoCargoDto(c, ComissaoCriterios.Preco(c), contratados.Contains(c))).ToList());
    }

    public async Task ContratarAsync(Guid teamId, CargoDaComissao cargo, CancellationToken ct)
    {
        var agora = HorarioDeBrasilia.Agora(_time);
        var (temporada, inicio) = await TemporadaAtualAsync(_db, agora, ct);
        if (temporada is not int t || t < ComissaoCriterios.ValeAPartirDaTemporada)
            throw new InvalidOperationException($"A comissão técnica começa na temporada {ComissaoCriterios.ValeAPartirDaTemporada}.");
        if (agora >= inicio!.Value.Date.AddDays(ComissaoCriterios.DiasParaContratar))
            throw new InvalidOperationException(
                $"A comissão se contrata nas {ComissaoCriterios.DiasParaContratar / 7} primeiras semanas da temporada: o prazo já passou.");
        if (await _db.ComissaoContratos.AnyAsync(c => c.TeamId == teamId && c.Temporada == t && c.Cargo == cargo, ct))
            throw new InvalidOperationException($"O {ComissaoDoClube.Nome(cargo).ToLowerInvariant()} já está contratado nesta temporada.");

        var time = await _db.Teams.FirstOrDefaultAsync(x => x.TeamId == teamId, ct)
            ?? throw new InvalidOperationException("Time não encontrado.");
        var preco = ComissaoCriterios.Preco(cargo);
        if (time.Budget - time.BudgetBlocked < preco)
            throw new InvalidOperationException($"Caixa insuficiente: o {ComissaoDoClube.Nome(cargo).ToLowerInvariant()} custa {Dinheiro(preco)}.");

        var utc = _time.GetUtcNow().UtcDateTime;
        time.Budget = decimal.Round(time.Budget - preco, 2, MidpointRounding.AwayFromZero);
        ExtratoCaixa.Lancar(_db, teamId, -preco, ExtratoCaixa.Comissao, $"Comissão técnica {t}: {ComissaoDoClube.Nome(cargo)}", utc);
        _db.ComissaoContratos.Add(new ComissaoContrato
        {
            ContratoId = Guid.NewGuid(), TeamId = teamId, Temporada = t, Cargo = cargo, Valor = preco, ContratadoEm = utc
        });
        await _db.SaveChangesAsync(ct);
    }

    // ── Para o treino e a virada ───────────────────────────────────────────────

    /// <summary>Cargos que o time tem contratados na temporada.</summary>
    public static async Task<HashSet<CargoDaComissao>> CargosAsync(DraftDbContext db, Guid teamId, int? temporada, CancellationToken ct) =>
        temporada is null
            ? []
            : (await db.ComissaoContratos.AsNoTracking()
                .Where(c => c.TeamId == teamId && c.Temporada == temporada)
                .Select(c => c.Cargo)
                .ToListAsync(ct))
            .ToHashSet();

    /// <summary>Times que contrataram o cargo na temporada.</summary>
    public static async Task<HashSet<Guid>> TimesComAsync(DraftDbContext db, int? temporada, CargoDaComissao cargo, CancellationToken ct) =>
        temporada is null
            ? []
            : (await db.ComissaoContratos.AsNoTracking()
                .Where(c => c.Temporada == temporada && c.Cargo == cargo)
                .Select(c => c.TeamId)
                .ToListAsync(ct))
            .ToHashSet();

    /// <summary>Temporada em andamento (a da Série A mais recente que já começou) e quando ela começou.</summary>
    public static async Task<(int? Temporada, DateTime? Inicio)> TemporadaAtualAsync(DraftDbContext db, DateTime agoraBrasilia, CancellationToken ct)
    {
        var liga = await db.Ligas.AsNoTracking()
            .Where(l => l.Tipo == TipoCompetition.Liga && l.DataInicio <= agoraBrasilia)
            .OrderByDescending(l => l.DataInicio)
            .Select(l => new { l.Temporada, l.DataInicio })
            .FirstOrDefaultAsync(ct);
        return (liga?.Temporada, liga?.DataInicio);
    }

    private static string Dinheiro(decimal valor) => $"R$ {valor / 1_000_000m:0.#} mi";
}
