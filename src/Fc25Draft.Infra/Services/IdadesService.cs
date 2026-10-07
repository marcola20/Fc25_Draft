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

    /// <summary>O save e o banco do PES têm as idades de 2008, quando a liga começou.</summary>
    private const int TemporadaDasIdadesDoJogo = 2008;

    /// <summary>
    /// Antes do botão existir, o admin deu +1 à mão nas idades do site (temporada 2009). Sem nenhum envelhecimento
    /// registrado, é nela que as idades estão.
    /// </summary>
    private const int TemporadaDasIdadesAntesDoBotao = 2009;

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

        var idades = await _db.Players.AsNoTracking().Select(p => p.Age).ToListAsync(ct);
        var comIdade = idades.Where(a => a is not null).Select(a => a!.Value).ToList();

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
            desta is not null && ultimo?.Temporada == temporada);
    }

    public async Task<IdadeDaLigaDto> GetIdadeDaLigaAsync(CancellationToken ct)
    {
        var ultima = await _db.EnvelhecimentosTemporada.AsNoTracking()
            .MaxAsync(e => (int?)e.Temporada, ct) ?? TemporadaDasIdadesAntesDoBotao;
        return new IdadeDaLigaDto(ultima, ultima - TemporadaDasIdadesDoJogo);
    }

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

            var jogadores = await _db.Players
                .Where(p => p.Age != null)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Age, p => p.Age + 1), ct);

            _db.EnvelhecimentosTemporada.Add(new EnvelhecimentoTemporada
            {
                Temporada = temporada,
                AplicadoEm = _time.GetUtcNow().UtcDateTime,
                Jogadores = jogadores
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

            await _db.Players
                .Where(p => p.Age != null)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Age, p => p.Age - 1), ct);

            _db.EnvelhecimentosTemporada.Remove(registro);
            await _db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        });

        return await GetSituacaoAsync(ct);
    }
}
