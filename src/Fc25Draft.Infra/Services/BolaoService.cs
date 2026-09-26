using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// O bolão da rodada. O palpite é da pessoa, então treinador e auxiliar do mesmo clube
/// disputam entre si, e quem está sem time também joga.
/// </summary>
public class BolaoService : IBolaoService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public BolaoService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    private DateTime Agora => _time.GetLocalNow().DateTime;

    public async Task<IReadOnlyList<BolaoRodadaDto>> RodadasAbertasAsync(Guid? treinadorId, CancellationToken ct)
    {
        var rodadas = await CarregarAsync(ct);

        return await MontarAsync(
            rodadas.Where(EstaAberta).OrderBy(r => r.Quando ?? DateTime.MaxValue).ThenBy(r => r.Numero),
            treinadorId, ct);
    }

    public async Task<IReadOnlyList<BolaoRodadaDto>> RodadasFechadasAsync(Guid? treinadorId, int quantas, CancellationToken ct)
    {
        var rodadas = await CarregarAsync(ct);

        return await MontarAsync(
            rodadas.Where(r => !EstaAberta(r))
                .OrderByDescending(r => r.Quando ?? DateTime.MinValue).ThenByDescending(r => r.Numero)
                .Take(quantas),
            treinadorId, ct);
    }

    public async Task<BolaoRodadaDto?> RodadaAsync(Guid rodadaId, Guid? treinadorId, CancellationToken ct)
    {
        var rodada = (await CarregarAsync(ct)).FirstOrDefault(r => r.RodadaId == rodadaId);
        if (rodada is null) return null;

        return (await MontarAsync(new[] { rodada }, treinadorId, ct)).Single();
    }

    public async Task<BolaoRodadaDto> SalvarAsync(
        Guid treinadorId, Guid rodadaId, IReadOnlyList<BolaoPalpiteRequest> palpites, CancellationToken ct)
    {
        var pessoa = await _db.Treinadores.AsNoTracking().FirstOrDefaultAsync(t => t.TreinadorId == treinadorId, ct)
            ?? throw new InvalidOperationException("Treinador não encontrado.");

        if (!pessoa.Ativo)
            throw new InvalidOperationException("Quem não está mais na liga não palpita.");

        var rodada = (await CarregarAsync(ct)).FirstOrDefault(r => r.RodadaId == rodadaId)
            ?? throw new InvalidOperationException("Rodada não encontrada.");

        if (!EstaAberta(rodada))
            throw new InvalidOperationException("Essa rodada já começou. Os palpites estão fechados.");

        var daRodada = rodada.Jogos.Select(j => j.PartidaId).ToHashSet();
        if (palpites.Any(p => !daRodada.Contains(p.PartidaId)))
            throw new InvalidOperationException("Tem palpite de jogo que não é dessa rodada.");

        if (palpites.Any(p => p.GolsCasa < 0 || p.GolsFora < 0 || p.GolsCasa > 99 || p.GolsFora > 99))
            throw new InvalidOperationException("Placar inválido.");

        var existentes = await _db.BolaoPalpites
            .Where(p => p.TreinadorId == treinadorId && daRodada.Contains(p.PartidaId))
            .ToListAsync(ct);

        foreach (var palpite in palpites)
        {
            var atual = existentes.FirstOrDefault(p => p.PartidaId == palpite.PartidaId);

            if (atual is null)
            {
                _db.BolaoPalpites.Add(new BolaoPalpite
                {
                    PalpiteId = Guid.NewGuid(),
                    TreinadorId = treinadorId,
                    PartidaId = palpite.PartidaId,
                    GolsCasa = palpite.GolsCasa,
                    GolsFora = palpite.GolsFora,
                    CriadoEm = Agora,
                    AtualizadoEm = Agora
                });
                continue;
            }

            if (atual.GolsCasa == palpite.GolsCasa && atual.GolsFora == palpite.GolsFora) continue;

            atual.GolsCasa = palpite.GolsCasa;
            atual.GolsFora = palpite.GolsFora;
            atual.AtualizadoEm = Agora;
        }

        await _db.SaveChangesAsync(ct);

        return (await RodadaAsync(rodadaId, treinadorId, ct))!;
    }

    public async Task<IReadOnlyList<BolaoPalpiteDeAlguemDto>> PalpitesDoJogoAsync(Guid partidaId, CancellationToken ct)
    {
        var rodada = (await CarregarAsync(ct)).FirstOrDefault(r => r.Jogos.Any(j => j.PartidaId == partidaId));
        if (rodada is null || EstaAberta(rodada)) return Array.Empty<BolaoPalpiteDeAlguemDto>();

        var jogo = rodada.Jogos.First(j => j.PartidaId == partidaId);

        var palpites = await _db.BolaoPalpites.AsNoTracking()
            .Where(p => p.PartidaId == partidaId)
            .Select(p => new { p.TreinadorId, p.Treinador.Nome, p.GolsCasa, p.GolsFora })
            .ToListAsync(ct);

        var clubes = await ClubesAtuaisAsync(ct);

        return palpites
            .Select(p => new BolaoPalpiteDeAlguemDto(
                p.TreinadorId, p.Nome, clubes.GetValueOrDefault(p.TreinadorId),
                p.GolsCasa, p.GolsFora,
                jogo.Encerrado ? BolaoPontuacao.Calcular(p.GolsCasa, p.GolsFora, jogo.GolsCasa, jogo.GolsFora) : null))
            .OrderByDescending(p => p.Pontos ?? 0).ThenBy(p => p.Nome)
            .ToArray();
    }

    public async Task<IReadOnlyList<BolaoRankingLinhaDto>> RankingAsync(int? temporada, CancellationToken ct)
    {
        var encerrados = (await CarregarAsync(ct))
            .SelectMany(r => r.Jogos.Where(j => j.Encerrado).Select(j => new { r.Temporada, Jogo = j }))
            .Where(x => temporada is null || x.Temporada == temporada)
            .ToDictionary(x => x.Jogo.PartidaId, x => x.Jogo);

        return await MontarRankingAsync(encerrados, ct);
    }

    public async Task<IReadOnlyList<BolaoRankingLinhaDto>> RankingDaRodadaAsync(Guid rodadaId, CancellationToken ct)
    {
        var rodada = (await CarregarAsync(ct)).FirstOrDefault(r => r.RodadaId == rodadaId);
        if (rodada is null) return Array.Empty<BolaoRankingLinhaDto>();

        return await MontarRankingAsync(rodada.Jogos.Where(j => j.Encerrado).ToDictionary(j => j.PartidaId), ct);
    }

    public async Task<IReadOnlyList<int>> TemporadasAsync(CancellationToken ct)
    {
        var comPalpite = await _db.BolaoPalpites.AsNoTracking().Select(p => p.PartidaId).Distinct().ToListAsync(ct);
        if (comPalpite.Count == 0) return Array.Empty<int>();

        var doBolao = comPalpite.ToHashSet();

        return (await CarregarAsync(ct))
            .Where(r => r.Temporada is not null && r.Jogos.Any(j => doBolao.Contains(j.PartidaId)))
            .Select(r => r.Temporada!.Value)
            .Distinct()
            .OrderByDescending(t => t)
            .ToArray();
    }

    public async Task<BolaoResumoDoTreinadorDto> ResumoAsync(Guid treinadorId, CancellationToken ct)
    {
        var geral = await RankingAsync(null, ct);
        var minha = geral.FirstOrDefault(l => l.TreinadorId == treinadorId);

        var rodadasVencidas = 0;
        foreach (var rodada in (await CarregarAsync(ct)).Where(r => r.Jogos.Any(j => j.Encerrado)))
        {
            var ranking = await RankingDaRodadaAsync(rodada.RodadaId, ct);
            if (ranking.FirstOrDefault() is { Pontos: > 0 } primeiro
                && ranking.Where(l => l.Pontos == primeiro.Pontos).Any(l => l.TreinadorId == treinadorId))
                rodadasVencidas++;
        }

        return new BolaoResumoDoTreinadorDto(
            minha?.Pontos ?? 0,
            minha?.Palpites ?? 0,
            minha?.Cravadas ?? 0,
            minha?.Posicao,
            rodadasVencidas);
    }

    // ── Bastidores ─────────────────────────────────────────────────────────────

    private record Jogo(Guid PartidaId, Guid CasaId, string Casa, Guid ForaId, string Fora, bool Encerrado, int GolsCasa, int GolsFora);

    private record Rodada(
        Guid RodadaId, Guid LigaId, string Competicao, TipoCompetition Tipo, int Numero, bool Desempate,
        int? Temporada, DateTime? Quando, IReadOnlyList<Jogo> Jogos);

    private List<Rodada>? _cache;

    /// <summary>Todas as rodadas com jogo marcado, já com placar e situação de cada partida.</summary>
    private async Task<List<Rodada>> CarregarAsync(CancellationToken ct)
    {
        if (_cache is not null) return _cache;

        var rodadas = await _db.LigaRodadas.AsNoTracking()
            .Select(r => new
            {
                r.RodadaId,
                r.LigaId,
                r.Numero,
                r.Desempate,
                r.DataHora,
                r.Liga.Tipo,
                r.Liga.Divisao,
                r.Liga.Temporada,
                Jogos = r.Partidas.Select(p => new
                {
                    p.PartidaId,
                    p.TimeCasaId,
                    Casa = p.TimeCasa.TeamName,
                    p.TimeForaId,
                    Fora = p.TimeFora.TeamName,
                    p.GolsCasa,
                    p.GolsFora,
                    p.IniciadaEm,
                    p.EncerradaEm,
                    p.Status
                }).ToList()
            })
            .ToListAsync(ct);

        _cache = rodadas
            .Where(r => r.Jogos.Count > 0)
            .Select(r => new Rodada(
                r.RodadaId, r.LigaId,
                LigaLabels.Competicao(r.Tipo, r.Divisao), r.Tipo, r.Numero, r.Desempate, r.Temporada, r.DataHora,
                r.Jogos.Select(j => new Jogo(
                    j.PartidaId, j.TimeCasaId, j.Casa, j.TimeForaId, j.Fora,
                    j.EncerradaEm is not null || j.Status == PartidaStatus.Encerrada,
                    j.GolsCasa, j.GolsFora)).ToList()))
            .ToList();

        return _cache;
    }

    /// <summary>
    /// A rodada aceita palpite enquanto a bola não rolar: antes do horário marcado e sem
    /// nenhum jogo começado. Rodada sem data fica aberta até o primeiro apito.
    /// </summary>
    private bool EstaAberta(Rodada rodada)
    {
        if (rodada.Jogos.Any(j => j.Encerrado)) return false;
        return rodada.Quando is not DateTime quando || Agora < quando;
    }

    private async Task<IReadOnlyList<BolaoRodadaDto>> MontarAsync(
        IEnumerable<Rodada> rodadas, Guid? treinadorId, CancellationToken ct)
    {
        var lista = rodadas.ToList();
        if (lista.Count == 0) return Array.Empty<BolaoRodadaDto>();

        var partidas = lista.SelectMany(r => r.Jogos.Select(j => j.PartidaId)).ToHashSet();

        var meus = new Dictionary<Guid, (int Casa, int Fora)>();
        if (treinadorId is Guid id)
        {
            var linhas = await _db.BolaoPalpites.AsNoTracking()
                .Where(p => p.TreinadorId == id && partidas.Contains(p.PartidaId))
                .Select(p => new { p.PartidaId, p.GolsCasa, p.GolsFora })
                .ToListAsync(ct);

            meus = linhas.ToDictionary(p => p.PartidaId, p => (p.GolsCasa, p.GolsFora));
        }

        return lista.Select(r => new BolaoRodadaDto(
            r.RodadaId, r.LigaId, r.Competicao, r.Tipo, r.Numero, Titulo(r), r.Quando, EstaAberta(r),
            r.Jogos.Select(j =>
            {
                var palpitou = meus.TryGetValue(j.PartidaId, out var meu);

                return new BolaoJogoDto(
                    j.PartidaId, j.CasaId, j.Casa, j.ForaId, j.Fora,
                    j.Encerrado,
                    j.Encerrado ? j.GolsCasa : null,
                    j.Encerrado ? j.GolsFora : null,
                    palpitou ? meu.Casa : null,
                    palpitou ? meu.Fora : null,
                    palpitou && j.Encerrado
                        ? BolaoPontuacao.Calcular(meu.Casa, meu.Fora, j.GolsCasa, j.GolsFora)
                        : null);
            }).ToList()))
            .ToArray();
    }

    private static string Titulo(Rodada r) => r.Numero switch
    {
        0 => $"{r.Competicao} · mata-mata",
        -1 => $"{r.Competicao} · decisão do título",
        -2 => $"{r.Competicao} · playoff",
        _ when r.Desempate => $"{r.Competicao} · jogo decisivo",
        _ => $"{r.Competicao} · rodada {r.Numero}"
    };

    private async Task<IReadOnlyList<BolaoRankingLinhaDto>> MontarRankingAsync(
        IReadOnlyDictionary<Guid, Jogo> jogos, CancellationToken ct)
    {
        if (jogos.Count == 0) return Array.Empty<BolaoRankingLinhaDto>();

        var ids = jogos.Keys.ToHashSet();

        var palpites = await _db.BolaoPalpites.AsNoTracking()
            .Where(p => ids.Contains(p.PartidaId))
            .Select(p => new { p.TreinadorId, p.Treinador.Nome, p.PartidaId, p.GolsCasa, p.GolsFora })
            .ToListAsync(ct);

        var clubes = await ClubesAtuaisAsync(ct);

        var linhas = palpites
            .GroupBy(p => new { p.TreinadorId, p.Nome })
            .Select(g =>
            {
                var pontos = g.Select(p => BolaoPontuacao.Calcular(
                    p.GolsCasa, p.GolsFora, jogos[p.PartidaId].GolsCasa, jogos[p.PartidaId].GolsFora)).ToList();

                return new
                {
                    g.Key.TreinadorId,
                    g.Key.Nome,
                    Pontos = pontos.Sum(),
                    Palpites = pontos.Count,
                    Cravadas = pontos.Count(p => p == BolaoPontuacao.PlacarExato),
                    Acertos = pontos.Count(p => p > 0)
                };
            })
            .OrderByDescending(l => l.Pontos).ThenByDescending(l => l.Cravadas).ThenBy(l => l.Nome)
            .ToList();

        return linhas
            .Select((l, i) => new BolaoRankingLinhaDto(
                i + 1, l.TreinadorId, l.Nome, clubes.GetValueOrDefault(l.TreinadorId),
                l.Pontos, l.Palpites, l.Cravadas, l.Acertos))
            .ToArray();
    }

    private async Task<Dictionary<Guid, string>> ClubesAtuaisAsync(CancellationToken ct) =>
        await _db.TreinadorPassagens.AsNoTracking()
            .Where(p => p.Ate == null)
            .Select(p => new { p.TreinadorId, p.Time.TeamName })
            .ToDictionaryAsync(p => p.TreinadorId, p => p.TeamName, ct);
}
