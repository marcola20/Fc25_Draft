using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Infra.Services;

public class SincronizacaoPesService : ISincronizacaoPesService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SincronizacaoPesService> _logger;

    public SincronizacaoPesService(DraftDbContext db, ILogger<SincronizacaoPesService> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<EvolucaoPesDto>> EvolucoesPendentesAsync(CancellationToken ct = default)
    {
        var pendentes = await _db.EvolucoesPes
            .AsNoTracking()
            .Where(e => e.AplicadaNoJogoEmUtc == null)
            .OrderBy(e => e.EvolucaoPesId)
            .Select(e => new
            {
                e.EvolucaoPesId, e.PlayerId, PesId = e.Player.Atributos == null ? null : e.Player.Atributos.PesId,
                e.Player.Name, e.Motivo, e.CriadaEmUtc, e.OverallAntes, e.OverallDepois, e.Mudancas,
            })
            .ToListAsync(ct);

        return pendentes
            .Select(e => new EvolucaoPesDto(e.EvolucaoPesId, e.PlayerId, e.PesId, e.Name, e.Motivo, e.CriadaEmUtc,
                e.OverallAntes, e.OverallDepois, e.Mudancas.Split(',').Select(int.Parse).ToArray()))
            .ToList();
    }

    public async Task<ResultadoSincronizacaoPesDto> SincronizarAsync(SincronizarPesDto dados, CancellationToken ct = default)
    {
        var aplicadasIds = dados.Aplicadas.Distinct().ToList();
        var itens = dados.Atributos;

        var repetidos = itens.GroupBy(i => i.PlayerId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (repetidos.Count > 0)
            throw new ArgumentException($"Jogador repetido na lista: {string.Join(", ", repetidos)}.");
        foreach (var item in itens)
        {
            if (item.Atributos is null || item.Atributos.Length != OverallPes.NumAtributos)
                throw new ArgumentException($"Jogador {item.PlayerId}: são {OverallPes.NumAtributos} atributos.");
            if (item.Atributos.Any(v => v is < AtributosPes.Minimo or > AtributosPes.Maximo))
                throw new ArgumentException($"Jogador {item.PlayerId}: atributo fora de {AtributosPes.Minimo}–{AtributosPes.Maximo}.");
            if (item.PosicaoPes is < 0 or > 12)
                throw new ArgumentException($"Jogador {item.PlayerId}: posição do PES inválida.");
            if (item.PeFracoUso is < 1 or > 4 || item.PeFracoPrecisao is < 1 or > 4)
                throw new ArgumentException($"Jogador {item.PlayerId}: pé fraco deve estar entre 1 e 4.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var aplicadas = await _db.EvolucoesPes
            .Where(e => aplicadasIds.Contains(e.EvolucaoPesId) && e.AplicadaNoJogoEmUtc == null)
            .ToListAsync(ct);
        foreach (var e in aplicadas)
            e.AplicadaNoJogoEmUtc = agora;

        var ids = itens.Select(i => i.PlayerId).ToList();
        var jogadores = await _db.Players
            .Include(p => p.Atributos)
            .Where(p => ids.Contains(p.PlayerId))
            .ToDictionaryAsync(p => p.PlayerId, ct);
        // Apagado ou sem atributos no site: fica de fora (e volta na resposta), sem travar o resto.
        var ignorados = ids.Where(id => !jogadores.TryGetValue(id, out var j) || j.Atributos is null).ToList();

        // O que foi feito no site depois do que o editor baixou continua valendo por cima do jogo.
        var aindaPendentes = await _db.EvolucoesPes
            .AsNoTracking()
            .Where(e => ids.Contains(e.PlayerId) && e.AplicadaNoJogoEmUtc == null)
            .Select(e => new { e.EvolucaoPesId, e.PlayerId, e.Mudancas })
            .ToListAsync(ct);
        var pendentePorJogador = aindaPendentes
            .Where(e => !aplicadasIds.Contains(e.EvolucaoPesId))
            .GroupBy(e => e.PlayerId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Mudancas.Split(',').Select(int.Parse).ToArray()).ToList());

        var overalls = new List<MudancaOverallDto>();
        foreach (var item in itens.Where(i => !ignorados.Contains(i.PlayerId)))
        {
            var jogador = jogadores[item.PlayerId];
            var dto = AtributosPes.ParaDto(jogador.Atributos!);
            var valores = item.Atributos.ToArray();
            foreach (var mudancas in pendentePorJogador.GetValueOrDefault(item.PlayerId) ?? [])
                for (var i = 0; i < valores.Length; i++)
                    valores[i] = Math.Clamp(valores[i] + mudancas[i], AtributosPes.Minimo, AtributosPes.Maximo);

            for (var i = 0; i < valores.Length; i++)
                AtributosPes.Todos[i].Set(dto, valores[i]);
            dto.PosicaoPes = item.PosicaoPes ?? dto.PosicaoPes;
            dto.PeFracoUso = item.PeFracoUso ?? dto.PeFracoUso;
            dto.PeFracoPrecisao = item.PeFracoPrecisao ?? dto.PeFracoPrecisao;
            AtributosPes.Aplicar(dto, jogador.Atributos!);

            var antes = jogador.Overall;
            if (OverallPes.Recalcular(jogador))
                overalls.Add(new MudancaOverallDto(jogador.PlayerId, jogador.Name, antes, jogador.Overall,
                    OverallPes.CalcularDoJogador(dto) ?? jogador.Overall));
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Sincronização com o PES: {Aplicadas} evoluções aplicadas no jogo, {Atualizados} jogadores com atributos do jogo, {Overalls} overalls mudaram.",
            aplicadas.Count, itens.Count - ignorados.Count, overalls.Count);
        return new ResultadoSincronizacaoPesDto(aplicadas.Count, itens.Count - ignorados.Count, overalls, ignorados);
    }

    public async Task<IReadOnlyList<MudancaOverallDto>> RecalcularTodosAsync(bool gravar, CancellationToken ct = default)
    {
        var jogadores = await _db.Players
            .Include(p => p.Atributos)
            .Where(p => p.Atributos != null)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var mudancas = new List<MudancaOverallDto>();
        foreach (var jogador in jogadores)
        {
            var atributos = jogador.Atributos!;
            var antes = jogador.Overall;
            if (OverallPes.Recalcular(jogador))
                mudancas.Add(new MudancaOverallDto(jogador.PlayerId, jogador.Name, antes, jogador.Overall,
                    OverallPes.CalcularDoJogador(AtributosPes.ParaDto(atributos)) ?? jogador.Overall));
        }

        if (gravar)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Overall pela fórmula do PES: {Mudaram} de {Total} jogadores com atributos mudaram.",
                mudancas.Count, jogadores.Count);
        }
        else
        {
            _db.ChangeTracker.Clear();
        }
        return mudancas;
    }
}
