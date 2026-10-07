using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Evolução de fim de temporada: a conta de cada jogador (curva G pela idade + desempenho), aplicada uma vez por
/// temporada nos atributos e no overall. Cada mudança vira uma <see cref="EvolucaoPes"/> que o Editor PES grava no save.
/// </summary>
public class EvolucaoTemporadaService : IEvolucaoTemporadaService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public EvolucaoTemporadaService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    // ── Prévia ─────────────────────────────────────────────────────────────

    public async Task<EvolucaoPreviaDto> GetPreviaAsync(int temporada, CancellationToken ct)
    {
        var registro = await _db.EvolucoesDaTemporada.AsNoTracking().FirstOrDefaultAsync(e => e.Temporada == temporada, ct);
        if (registro is null)
            return new EvolucaoPreviaDto(temporada, null, false, (await CalcularAsync(temporada, ct)).Select(c => c.Linha).ToArray());

        var linhas = await _db.VariacoesDaTemporada.AsNoTracking()
            .Where(v => v.Temporada == temporada)
            .Select(v => new EvolucaoLinhaDto(
                v.PlayerId, v.Player.Name, v.Player.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault(),
                v.Player.Position.Name, v.Idade, v.OverallAntes, v.OverallDepois, v.Variacao, v.Curva, v.Desempenho,
                v.JogosDoClube, v.Titular, v.NotaMedia, v.Explicacao))
            .ToListAsync(ct);

        var evolucaoIds = await _db.VariacoesDaTemporada.AsNoTracking()
            .Where(v => v.Temporada == temporada && v.EvolucaoPesId != null)
            .Select(v => v.EvolucaoPesId!.Value)
            .ToListAsync(ct);
        var jaNoJogo = await _db.EvolucoesPes.AsNoTracking()
            .AnyAsync(e => evolucaoIds.Contains(e.EvolucaoPesId) && e.AplicadaNoJogoEmUtc != null, ct);

        return new EvolucaoPreviaDto(temporada, registro.AplicadaEm, !jaNoJogo, Ordenar(linhas));
    }

    private sealed record Calculo(EvolucaoLinhaDto Linha, DesempenhoTemporada Desempenho);

    /// <summary>A conta de todos os jogadores com idade, pelos jogos da temporada.</summary>
    private async Task<IReadOnlyList<Calculo>> CalcularAsync(int temporada, CancellationToken ct)
    {
        // A idade que vale é a da temporada: se a próxima já fez aniversário, desconta.
        var aniversariosDepois = await _db.EnvelhecimentosTemporada.AsNoTracking().CountAsync(e => e.Temporada > temporada, ct);

        var jogadores = await _db.Players.AsNoTracking()
            .Where(p => p.Age != null && p.AposentadoNaTemporada == null)
            .Select(p => new
            {
                p.PlayerId, p.Name, Idade = p.Age!.Value - aniversariosDepois, p.Overall, p.PositionId, Posicao = p.Position.Name,
                PosicaoPes = p.Atributos != null ? p.Atributos.PosicaoPes : null,
                TimeId = p.TeamRosters.Select(r => (Guid?)r.TeamId).FirstOrDefault(),
                TimeNome = p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault()
            })
            .ToListAsync(ct);

        var partidas = await _db.LigaPartidas.AsNoTracking()
            .Where(p => p.Status == PartidaStatus.Encerrada && p.Rodada.Liga.Temporada == temporada)
            .Select(p => new { p.PartidaId, Quando = p.EncerradaEm ?? p.Rodada.DataHora })
            .ToListAsync(ct);
        var ids = partidas.Select(p => p.PartidaId).ToList();
        var quando = partidas.ToDictionary(p => p.PartidaId, p => p.Quando);

        // Escalação gravada no fim do jogo: só os titulares. É ela que diz quem começou e quais jogos do clube contam.
        var titulares = await _db.LigaEscalacoes.AsNoTracking()
            .Where(e => ids.Contains(e.PartidaId) && e.Titular)
            .Select(e => new { e.PartidaId, e.TimeId, e.JogadorId })
            .ToListAsync(ct);
        var jogosComEscalacao = titulares
            .Select(t => (t.TimeId, t.PartidaId)).Distinct()
            .GroupBy(x => x.TimeId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.PartidaId).ToList());
        var titularesPorJogador = titulares.GroupBy(t => t.JogadorId).ToDictionary(g => g.Key, g => g.ToList());

        var notas = (await _db.LigaNotasJogadores.AsNoTracking()
                .Where(n => ids.Contains(n.PartidaId))
                .Select(n => new { n.JogadorId, n.Nota })
                .ToListAsync(ct))
            .GroupBy(n => n.JogadorId)
            .ToDictionary(g => g.Key, g => (Media: Math.Round(g.Average(n => n.Nota), 2), Jogos: g.Count()));

        // Quando cada um chegou ao clube atual: jogo do clube antes disso não conta contra ele.
        var chegadas = (await _db.TransferHistories.AsNoTracking()
                .Where(t => t.ToTeamId != null)
                .Select(t => new { t.PlayerId, t.ToTeamId, t.PerformedAtUtc })
                .ToListAsync(ct))
            .GroupBy(t => (t.PlayerId, t.ToTeamId!.Value))
            .ToDictionary(g => g.Key, g => g.Max(t => t.PerformedAtUtc));

        var resultado = new List<Calculo>(jogadores.Count);
        foreach (var j in jogadores)
        {
            var dele = titularesPorJogador.GetValueOrDefault(j.PlayerId) ?? [];
            var jogosDoClube = 0;
            if (j.TimeId is Guid time)
            {
                var desde = chegadas.TryGetValue((j.PlayerId, time), out var c) ? c : (DateTime?)null;
                jogosDoClube = (jogosComEscalacao.GetValueOrDefault(time) ?? [])
                    .Count(pid => desde is null || quando[pid] is not DateTime q || q >= desde);
                // Jogos que ele começou por outro clube na temporada (antes de ser transferido).
                jogosDoClube += dele.Count(t => t.TimeId != time);
            }

            var nota = notas.TryGetValue(j.PlayerId, out var n) ? n : default;
            var desempenho = new DesempenhoTemporada(
                j.TimeId is null, jogosDoClube, dele.Count, nota.Jogos > 0 ? nota.Media : null, nota.Jogos);
            var goleiro = j.PosicaoPes is int pp ? pp == 0 : j.PositionId == (short)PositionType.Goleiro;
            var conta = EvolucaoTemporada.Calcular(j.Idade, goleiro, desempenho);
            var depois = Math.Clamp(j.Overall + conta.Total, AtributosPes.Minimo, AtributosPes.Maximo);

            resultado.Add(new Calculo(
                new EvolucaoLinhaDto(j.PlayerId, j.Name, j.TimeNome, j.Posicao, j.Idade, j.Overall, depois, depois - j.Overall,
                    conta.Curva, conta.Desempenho, jogosDoClube, dele.Count, desempenho.NotaMedia, string.Join(" · ", conta.Motivos)),
                desempenho));
        }

        return resultado.OrderByDescending(r => r.Linha.Variacao).ThenByDescending(r => r.Linha.OverallAntes).ToArray();
    }

    private static IReadOnlyList<EvolucaoLinhaDto> Ordenar(IEnumerable<EvolucaoLinhaDto> linhas) =>
        linhas.OrderByDescending(l => l.Variacao).ThenByDescending(l => l.OverallAntes).ToArray();

    // ── Aplicar e desfazer ─────────────────────────────────────────────────

    public async Task<EvolucaoPreviaDto> AplicarAsync(int temporada, CancellationToken ct)
    {
        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transacao = await _db.Database.BeginTransactionAsync(ct);

            if (await _db.EvolucoesDaTemporada.AnyAsync(e => e.Temporada == temporada, ct))
                throw new InvalidOperationException($"A evolução da temporada {temporada} já foi aplicada.");

            var calculos = await CalcularAsync(temporada, ct);
            var agora = _time.GetUtcNow().UtcDateTime;

            var mudam = calculos.Where(c => c.Linha.Variacao != 0).Select(c => c.Linha.PlayerId).ToList();
            var jogadores = await _db.Players.Include(p => p.Atributos)
                .Where(p => mudam.Contains(p.PlayerId))
                .ToDictionaryAsync(p => p.PlayerId, ct);

            var evolucoes = new Dictionary<int, EvolucaoPes>();
            foreach (var c in calculos.Where(c => c.Linha.Variacao != 0))
            {
                var jogador = jogadores[c.Linha.PlayerId];
                var antesOverall = jogador.Overall;

                if (jogador.Atributos?.PosicaoPes is int pos)
                {
                    var dto = AtributosPes.ParaDto(jogador.Atributos);
                    var antes = OverallPes.Valores(dto);
                    var alvo = c.Linha.OverallDepois;
                    var evolucao = c.Linha.Variacao > 0
                        ? OverallPes.Evoluir(antes, pos, dto.EstiloDeJogo, dto.PeFracoUso, dto.PeFracoPrecisao, alvo)
                        : OverallPes.Regredir(antes, pos, dto.PeFracoUso, dto.PeFracoPrecisao, alvo);
                    if (evolucao.Novos.SequenceEqual(antes)) continue;

                    for (var i = 0; i < antes.Length; i++)
                        AtributosPes.Todos[i].Set(dto, evolucao.Novos[i]);
                    AtributosPes.Aplicar(dto, jogador.Atributos);
                    OverallPes.Recalcular(jogador);

                    var e = new EvolucaoPes
                    {
                        PlayerId = jogador.PlayerId,
                        Motivo = $"Temporada {temporada}: {EvolucaoTemporada.Sinal(c.Linha.Variacao)}",
                        CriadaEmUtc = agora,
                        OverallAntes = antesOverall,
                        OverallDepois = jogador.Overall,
                        Mudancas = EvolucaoPes.EscreverMudancas(antes, evolucao.Novos),
                    };
                    _db.EvolucoesPes.Add(e);
                    evolucoes[jogador.PlayerId] = e;
                }
                else
                {
                    // Sem atributos do jogo: só o overall do site muda.
                    jogador.Overall = c.Linha.OverallDepois;
                }
            }

            await _db.SaveChangesAsync(ct);

            foreach (var c in calculos)
            {
                var depois = jogadores.TryGetValue(c.Linha.PlayerId, out var j) ? j.Overall : c.Linha.OverallAntes;
                _db.VariacoesDaTemporada.Add(new VariacaoDaTemporada
                {
                    Id = Guid.NewGuid(),
                    Temporada = temporada,
                    PlayerId = c.Linha.PlayerId,
                    Idade = c.Linha.Idade,
                    SemClube = c.Desempenho.SemClube,
                    JogosDoClube = c.Desempenho.JogosDoClube,
                    Titular = c.Desempenho.Titular,
                    NotaMedia = c.Desempenho.NotaMedia,
                    Curva = c.Linha.Curva,
                    Desempenho = c.Linha.Desempenho,
                    Variacao = depois - c.Linha.OverallAntes,
                    OverallAntes = c.Linha.OverallAntes,
                    OverallDepois = depois,
                    Explicacao = c.Linha.Explicacao,
                    EvolucaoPesId = evolucoes.TryGetValue(c.Linha.PlayerId, out var e) ? e.EvolucaoPesId : null
                });
            }

            _db.EvolucoesDaTemporada.Add(new EvolucaoDaTemporada
            {
                Temporada = temporada,
                AplicadaEm = agora,
                Jogadores = calculos.Count(c => c.Linha.Variacao != 0)
            });

            await _db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        });

        return await GetPreviaAsync(temporada, ct);
    }

    public async Task<EvolucaoPreviaDto> DesfazerAsync(int temporada, CancellationToken ct)
    {
        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transacao = await _db.Database.BeginTransactionAsync(ct);

            var registro = await _db.EvolucoesDaTemporada.FirstOrDefaultAsync(e => e.Temporada == temporada, ct)
                ?? throw new InvalidOperationException($"A evolução da temporada {temporada} não foi aplicada.");

            var variacoes = await _db.VariacoesDaTemporada.Where(v => v.Temporada == temporada).ToListAsync(ct);
            var evolucaoIds = variacoes.Where(v => v.EvolucaoPesId != null).Select(v => v.EvolucaoPesId!.Value).ToList();
            var evolucoes = await _db.EvolucoesPes.Where(e => evolucaoIds.Contains(e.EvolucaoPesId)).ToDictionaryAsync(e => e.EvolucaoPesId, ct);

            if (evolucoes.Values.Any(e => e.AplicadaNoJogoEmUtc != null))
                throw new InvalidOperationException(
                    "O Editor PES já gravou parte dessa evolução no jogo: desfazer agora deixaria o site e o jogo diferentes.");

            var mudaram = variacoes.Where(v => v.Variacao != 0).Select(v => v.PlayerId).ToList();
            var jogadores = await _db.Players.Include(p => p.Atributos)
                .Where(p => mudaram.Contains(p.PlayerId))
                .ToDictionaryAsync(p => p.PlayerId, ct);

            foreach (var v in variacoes.Where(v => v.Variacao != 0))
            {
                if (!jogadores.TryGetValue(v.PlayerId, out var jogador)) continue;

                if (v.EvolucaoPesId is int id && evolucoes.TryGetValue(id, out var e) && jogador.Atributos is not null)
                {
                    var dto = AtributosPes.ParaDto(jogador.Atributos);
                    var atuais = OverallPes.Valores(dto);
                    var mudancas = e.LerMudancas();
                    for (var i = 0; i < atuais.Length; i++)
                        AtributosPes.Todos[i].Set(dto, Math.Clamp(atuais[i] - mudancas[i], AtributosPes.Minimo, AtributosPes.Maximo));
                    AtributosPes.Aplicar(dto, jogador.Atributos);
                    OverallPes.Recalcular(jogador);
                    _db.EvolucoesPes.Remove(e);
                }
                else
                {
                    jogador.Overall -= v.Variacao;
                }
            }

            _db.VariacoesDaTemporada.RemoveRange(variacoes);
            _db.EvolucoesDaTemporada.Remove(registro);
            await _db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        });

        return await GetPreviaAsync(temporada, ct);
    }
}
