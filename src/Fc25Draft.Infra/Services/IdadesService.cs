using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// O aniversário de todos os jogadores, uma vez por temporada, com trava para não somar dois anos.
/// A idade fica no próprio <see cref="Player.Age"/>; o Editor PES copia do site para o save.
/// </summary>
public class IdadesService : IIdadesService
{
    /// <summary>Até esta idade o jogador entra no draft (ver docs/ciclo-de-vida-dos-jogadores.md).</summary>
    private const int IdadeMaximaDoDraft = 23;
    private const int IdadeDeFimDeCarreira = 34;

    /// <summary>
    /// As idades da base do PES embutida são as da temporada 2009 da liga: na temporada T, a idade certa é a da
    /// base + (T − 2009). Quem já está nela (ex.: entrou no site depois, já com +1) não ganha o ano.
    /// </summary>
    private const int TemporadaDaBaseDoPes = 2009;

    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public IdadesService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<EnvelhecimentoSituacaoDto> GetSituacaoAsync(CancellationToken ct)
    {
        var temporada = await _db.Ligas.AsNoTracking()
            .Where(l => l.Temporada != null && l.Tipo == TipoCompetition.Liga)
            .MaxAsync(l => l.Temporada, ct);

        var registros = await _db.EnvelhecimentosTemporada.AsNoTracking()
            .OrderByDescending(e => e.Temporada)
            .ToListAsync(ct);
        var desta = registros.FirstOrDefault(e => e.Temporada == temporada);
        var ultimo = registros.FirstOrDefault();

        var jogadores = await JogadoresAsync(ct);
        var idades = jogadores.Select(j => j.Age).ToList();
        var comIdade = idades.Where(a => a is not null).Select(a => a!.Value).ToList();
        var jaNaIdade = desta is null && temporada is int t
            ? jogadores.Where(j => JaNaIdade(j, t)).Select(j => j.Nome).OrderBy(n => n).ToArray()
            : Array.Empty<string>();

        return new EnvelhecimentoSituacaoDto(
            temporada,
            desta?.AplicadoEm,
            desta?.Jogadores,
            comIdade.Count,
            idades.Count - comIdade.Count,
            comIdade.Count == 0 ? 0 : Math.Round(comIdade.Average(), 1),
            comIdade.Count(a => a == IdadeMaximaDoDraft),
            comIdade.Count(a => a == IdadeDeFimDeCarreira - 1),
            ultimo?.Temporada,
            desta is not null && ultimo?.Temporada == temporada,
            jaNaIdade);
    }

    private sealed record JogadorIdade(int PlayerId, string Nome, int? Age, int? PesId);

    private async Task<List<JogadorIdade>> JogadoresAsync(CancellationToken ct) =>
        await _db.Players.AsNoTracking()
            .Select(p => new JogadorIdade(p.PlayerId, p.Name, p.Age, p.Atributos != null ? p.Atributos.PesId : null))
            .ToListAsync(ct);

    /// <summary>Já tem a idade da temporada pela base do PES. Sem ligação com a base, ganha o ano normalmente.</summary>
    private static bool JaNaIdade(JogadorIdade j, int temporada) =>
        j.Age is int idade
        && j.PesId is int pesId
        && BasePesService.IdadeNaBase(pesId) is int naBase
        && idade >= naBase + (temporada - TemporadaDaBaseDoPes);

    public async Task<EnvelhecimentoSituacaoDto> EnvelhecerAsync(int temporada, CancellationToken ct)
    {
        // A conexão repete em caso de falha: a transação precisa rodar dentro da estratégia dela.
        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transacao = await _db.Database.BeginTransactionAsync(ct);
            if (await _db.EnvelhecimentosTemporada.AnyAsync(e => e.Temporada == temporada, ct))
                throw new InvalidOperationException($"Os jogadores já fizeram aniversário na temporada {temporada}.");
            if (await _db.EnvelhecimentosTemporada.AnyAsync(e => e.Temporada > temporada, ct))
                throw new InvalidOperationException($"Já há uma temporada depois de {temporada} envelhecida.");

            var todos = await JogadoresAsync(ct);
            var pulados = todos.Where(j => JaNaIdade(j, temporada)).Select(j => j.PlayerId).ToList();
            var ganham = todos.Where(j => j.Age is not null && !pulados.Contains(j.PlayerId)).Select(j => j.PlayerId).ToList();

            var jogadores = await _db.Players
                .Where(p => ganham.Contains(p.PlayerId))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Age, p => p.Age + 1), ct);

            _db.EnvelhecimentosTemporada.Add(new EnvelhecimentoTemporada
            {
                Temporada = temporada,
                AplicadoEm = _time.GetUtcNow().UtcDateTime,
                Jogadores = jogadores,
                Pulados = pulados.Count == 0 ? null : string.Join(",", pulados)
            });
            await _db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        });

        return await GetSituacaoAsync(ct);
    }

    public async Task<EnvelhecimentoSituacaoDto> DesfazerAsync(int temporada, CancellationToken ct)
    {
        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transacao = await _db.Database.BeginTransactionAsync(ct);
            var registro = await _db.EnvelhecimentosTemporada.FirstOrDefaultAsync(e => e.Temporada == temporada, ct)
                ?? throw new InvalidOperationException($"A temporada {temporada} não foi envelhecida.");
            if (await _db.EnvelhecimentosTemporada.AnyAsync(e => e.Temporada > temporada, ct))
                throw new InvalidOperationException("Só dá para desfazer o último envelhecimento.");

            var pulados = (registro.Pulados ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
            await _db.Players
                .Where(p => p.Age != null && !pulados.Contains(p.PlayerId))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Age, p => p.Age - 1), ct);

            _db.EnvelhecimentosTemporada.Remove(registro);
            await _db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        });

        return await GetSituacaoAsync(ct);
    }
}
