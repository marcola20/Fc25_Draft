using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Extensions;
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

    // As rodadas guardam o horário de Brasília; o servidor (Render) roda em UTC.
    private DateTime Agora => HorarioDeBrasilia.Agora(_time);

    public async Task<IReadOnlyList<BolaoRodadaDto>> RodadasAbertasAsync(Guid? treinadorId, CancellationToken ct)
    {
        var rodadas = await CarregarAsync(ct);

        // Pelo próximo jogo que ainda aceita palpite: a rodada do mata-mata junta as fases e não tem data própria.
        return await MontarAsync(
            rodadas.Where(EstaAberta)
                .OrderBy(r => r.Jogos.Where(JogoAberto).Min(j => j.Quando) ?? DateTime.MaxValue)
                .ThenBy(r => r.Numero),
            treinadorId, ct);
    }

    public async Task<IReadOnlyList<BolaoRodadaDto>> RodadasFechadasAsync(Guid? treinadorId, int quantas, CancellationToken ct)
    {
        var rodadas = await CarregarAsync(ct);

        // Pelo último jogo: a rodada do mata-mata não tem data própria, e os jogos dele, às vezes, também não.
        return await MontarAsync(
            rodadas.Where(r => !EstaAberta(r))
                .OrderByDescending(r => r.Jogos.Max(j => j.Quando ?? j.EncerradaEm) ?? r.Quando ?? DateTime.MinValue)
                .ThenByDescending(r => r.Numero)
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

        var jogos = rodada.Jogos.ToDictionary(j => j.PartidaId);
        if (palpites.Any(p => !jogos.ContainsKey(p.PartidaId)))
            throw new InvalidOperationException("Tem palpite de jogo que não é dessa rodada.");

        // Cada jogo fecha no seu horário: o que já começou fica de fora, e o resto da rodada vale.
        palpites = palpites.Where(p => JogoAberto(jogos[p.PartidaId])).ToList();
        if (palpites.Count == 0)
            throw new InvalidOperationException("Esses jogos já começaram. Os palpites deles estão fechados.");

        if (palpites.Any(p => p.GolsCasa < 0 || p.GolsFora < 0 || p.GolsCasa > 99 || p.GolsFora > 99))
            throw new InvalidOperationException("Placar inválido.");

        var daRodada = jogos.Keys.ToList();
        var existentes = await _db.BolaoPalpites
            .Where(p => p.TreinadorId == treinadorId && daRodada.Contains(p.PartidaId))
            .ToListAsync(ct);

        // Aposta G/A: o jogador tem de estar no elenco do time daquele lado. Quem já estava escolhido
        // continua valendo mesmo se saiu do clube depois (aí ele não entra em campo e a aposta não conta).
        var escolhidos = palpites.SelectMany(p => new[] { p.JogadorCasaId, p.JogadorForaId }).OfType<int>().Distinct().ToList();
        var jogadores = escolhidos.Count == 0
            ? new Dictionary<int, (string Nome, Guid? Time)>()
            : (await _db.Players.AsNoTracking()
                .Where(p => escolhidos.Contains(p.PlayerId))
                .Select(p => new { p.PlayerId, p.Name, p.CurrentTeamId, p.AposentadoNaTemporada })
                .ToListAsync(ct))
                .ToDictionary(p => p.PlayerId, p => (Nome: p.Name, Time: p.AposentadoNaTemporada is null ? p.CurrentTeamId : null));

        foreach (var palpite in palpites)
        {
            var jogo = jogos[palpite.PartidaId];
            var atual = existentes.FirstOrDefault(p => p.PartidaId == palpite.PartidaId);

            // Chutou 0 para o time: a aposta daquele lado é o clean sheet, não tem jogador.
            var jogadorCasa = jogo.RegrasNovas && palpite.GolsCasa > 0 ? palpite.JogadorCasaId : null;
            var jogadorFora = jogo.RegrasNovas && palpite.GolsFora > 0 ? palpite.JogadorForaId : null;
            Conferir(jogadorCasa, jogo.CasaId, jogo.Casa, atual?.JogadorCasaId);
            Conferir(jogadorFora, jogo.ForaId, jogo.Fora, atual?.JogadorForaId);

            // Quem passa nos pênaltis: só no jogo que precisa de vencedor e com empate no palpite.
            var penaltis = jogo.Eliminatorio && palpite.GolsCasa == palpite.GolsFora ? palpite.PenaltisVencedorId : null;
            if (penaltis is Guid time && time != jogo.CasaId && time != jogo.ForaId)
                throw new InvalidOperationException("O time dos pênaltis não é desse jogo.");

            if (atual is null)
            {
                _db.BolaoPalpites.Add(new BolaoPalpite
                {
                    PalpiteId = Guid.NewGuid(),
                    TreinadorId = treinadorId,
                    PartidaId = palpite.PartidaId,
                    GolsCasa = palpite.GolsCasa,
                    GolsFora = palpite.GolsFora,
                    JogadorCasaId = jogadorCasa,
                    JogadorForaId = jogadorFora,
                    PenaltisVencedorId = penaltis,
                    CriadoEm = Agora,
                    AtualizadoEm = Agora
                });
                continue;
            }

            if (atual.GolsCasa == palpite.GolsCasa && atual.GolsFora == palpite.GolsFora
                && atual.JogadorCasaId == jogadorCasa && atual.JogadorForaId == jogadorFora
                && atual.PenaltisVencedorId == penaltis) continue;

            atual.GolsCasa = palpite.GolsCasa;
            atual.GolsFora = palpite.GolsFora;
            atual.JogadorCasaId = jogadorCasa;
            atual.JogadorForaId = jogadorFora;
            atual.PenaltisVencedorId = penaltis;
            atual.AtualizadoEm = Agora;
        }

        await _db.SaveChangesAsync(ct);

        return (await RodadaAsync(rodadaId, treinadorId, ct))!;

        void Conferir(int? jogadorId, Guid timeId, string time, int? jaEscolhido)
        {
            if (jogadorId is not int id || id == jaEscolhido) return;
            if (!jogadores.TryGetValue(id, out var jogador))
                throw new InvalidOperationException("Jogador não encontrado.");
            if (jogador.Time != timeId)
                throw new InvalidOperationException($"{jogador.Nome} não é do {time}.");
        }
    }

    public async Task<IReadOnlyList<BolaoPalpiteDeAlguemDto>> PalpitesDoJogoAsync(Guid partidaId, CancellationToken ct)
    {
        var jogo = (await CarregarAsync(ct)).SelectMany(r => r.Jogos).FirstOrDefault(j => j.PartidaId == partidaId);
        if (jogo is null || JogoAberto(jogo)) return Array.Empty<BolaoPalpiteDeAlguemDto>();

        var palpites = await PalpitesAsync(new[] { partidaId }, null, ct);
        var lances = await LancesAsync(new[] { jogo }, ct);
        var clubes = await ClubesAtuaisAsync(ct);

        return palpites
            .Select(p => new BolaoPalpiteDeAlguemDto(
                p.TreinadorId, p.Nome, clubes.GetValueOrDefault(p.TreinadorId),
                p.GolsCasa, p.GolsFora,
                jogo.Encerrado ? Apurar(p, jogo, lances).Total : null,
                p.JogadorCasa, p.JogadorFora, NomeDoTime(jogo, p.PenaltisId)))
            .OrderByDescending(p => p.Pontos ?? 0).ThenBy(p => p.Nome)
            .ToArray();
    }

    public async Task<BolaoPalpitesDaRodadaDto> PalpitesDaRodadaAsync(Guid rodadaId, CancellationToken ct)
    {
        var rodada = (await CarregarAsync(ct)).FirstOrDefault(r => r.RodadaId == rodadaId);
        if (rodada is null) return new(Array.Empty<BolaoPalpitesDeAlguemDto>(), Array.Empty<string>());

        var jogos = rodada.Jogos.ToDictionary(j => j.PartidaId);
        var palpites = await PalpitesAsync(jogos.Keys, null, ct);
        var lances = await LancesAsync(rodada.Jogos, ct);
        var clubes = await ClubesAtuaisAsync(ct);

        var pessoas = palpites
            .GroupBy(p => new { p.TreinadorId, p.Nome })
            .Select(g => new BolaoPalpitesDeAlguemDto(
                g.Key.TreinadorId, g.Key.Nome, clubes.GetValueOrDefault(g.Key.TreinadorId),
                g.Select(p => new BolaoPalpiteNoJogoDto(
                    p.PartidaId, p.GolsCasa, p.GolsFora, p.JogadorCasa, p.JogadorFora,
                    jogos[p.PartidaId].Encerrado ? Apurar(p, jogos[p.PartidaId], lances).Total : null,
                    NomeDoTime(jogos[p.PartidaId], p.PenaltisId))).ToList()))
            .OrderBy(p => p.Nome)
            .ToList();

        var palpitaram = pessoas.Select(p => p.TreinadorId).ToList();
        var faltam = await _db.Treinadores.AsNoTracking()
            .Where(t => t.Ativo && !palpitaram.Contains(t.TreinadorId))
            .OrderBy(t => t.Nome)
            .Select(t => t.Nome)
            .ToListAsync(ct);

        return new(pessoas, faltam);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<BolaoJogadorOpcaoDto>>> ElencosAsync(
        IEnumerable<Guid> timeIds, CancellationToken ct)
    {
        var ids = timeIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, IReadOnlyList<BolaoJogadorOpcaoDto>>();

        var jogadores = await _db.Players.AsNoTracking()
            .Where(p => p.CurrentTeamId != null && ids.Contains(p.CurrentTeamId.Value) && p.AposentadoNaTemporada == null)
            .Select(p => new { p.PlayerId, p.Name, p.PositionId, p.Overall, Time = p.CurrentTeamId!.Value })
            .ToListAsync(ct);

        return ids.ToDictionary(
            id => id,
            id => (IReadOnlyList<BolaoJogadorOpcaoDto>)jogadores
                .Where(j => j.Time == id)
                .OrderBy(j => OrdemNoAtaque(j.PositionId)).ThenByDescending(j => j.Overall).ThenBy(j => j.Name)
                .Select(j => new BolaoJogadorOpcaoDto(j.PlayerId, j.Name, ((int)j.PositionId).ToPositionSigla()))
                .ToList());
    }

    public async Task<IReadOnlyDictionary<Guid, int>> PontosPorPessoaAsync(IReadOnlyCollection<Guid> partidaIds, CancellationToken ct)
    {
        var pedidos = partidaIds.ToHashSet();
        var jogos = (await CarregarAsync(ct))
            .SelectMany(r => r.Jogos)
            .Where(j => j.Encerrado && pedidos.Contains(j.PartidaId))
            .ToDictionary(j => j.PartidaId);
        if (jogos.Count == 0) return new Dictionary<Guid, int>();

        var palpites = await PalpitesAsync(jogos.Keys, null, ct);
        var lances = await LancesAsync(jogos.Values, ct);

        return palpites
            .GroupBy(p => p.TreinadorId)
            .ToDictionary(g => g.Key, g => g.Sum(p => Apurar(p, jogos[p.PartidaId], lances).Total));
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

    /// <param name="Quando">Horário do jogo: o próprio ou, sem ele, o da rodada.</param>
    /// <param name="Eliminatorio">Jogo único que precisa de vencedor: empatado, vai para os pênaltis.</param>
    /// <param name="PenaltisVencedorId">Quem passou nos pênaltis, quando o jogo foi para eles.</param>
    /// <param name="EncerradaEm">Quando o jogo acabou (Brasília).</param>
    private record Jogo(
        Guid PartidaId, Guid CasaId, string Casa, Guid ForaId, string Fora, bool Comecou, bool Encerrado,
        int GolsCasa, int GolsFora, bool IsWO, bool RegrasNovas, DateTime? Quando,
        bool Eliminatorio = false, Guid? PenaltisVencedorId = null, DateTime? EncerradaEm = null);

    private record Rodada(
        Guid RodadaId, Guid LigaId, string Competicao, TipoCompetition Tipo, int Numero, bool Desempate,
        int? Temporada, DateTime? Quando, IReadOnlyList<Jogo> Jogos);

    private record Palpite(
        Guid TreinadorId, string Nome, Guid PartidaId, int GolsCasa, int GolsFora,
        int? JogadorCasaId, string? JogadorCasa, int? JogadorForaId, string? JogadorFora, Guid? PenaltisId);

    /// <summary>Quem fez gol ou deu assistência no jogo, quem entrou em campo e de que times há retrato da escalação.</summary>
    private sealed record Lances(HashSet<int> ComGA, HashSet<int> EmCampo, HashSet<Guid> TimesComEscalacao)
    {
        public static readonly Lances Vazio = new(new(), new(), new());
    }

    private sealed record Apuracao(int Placar, int? ExtraCasa, int? ExtraFora, int? Penaltis)
    {
        public int Total => Placar + (ExtraCasa ?? 0) + (ExtraFora ?? 0) + (Penaltis ?? 0);
    }

    private List<Rodada>? _cache;
    private readonly Dictionary<Guid, Lances> _lances = new();

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
                    p.IsWO,
                    p.IniciadaEm,
                    p.EncerradaEm,
                    p.Status,
                    p.DataHora,
                    p.TemPenaltis,
                    p.PenaltisVencedorId
                }).ToList()
            })
            .ToListAsync(ct);

        var doMataMata = (await _db.LigaKnockoutJogos.AsNoTracking()
            .Where(k => k.PartidaId != null)
            .Select(k => k.PartidaId!.Value)
            .ToListAsync(ct))
            .ToHashSet();

        _cache = rodadas
            .Where(r => r.Jogos.Count > 0)
            .Select(r => new Rodada(
                r.RodadaId, r.LigaId,
                LigaLabels.Competicao(r.Tipo, r.Divisao), r.Tipo, r.Numero, r.Desempate, r.Temporada, r.DataHora,
                r.Jogos.Select(j => new Jogo(
                    j.PartidaId, j.TimeCasaId, j.Casa, j.TimeForaId, j.Fora,
                    j.IniciadaEm is not null || j.EncerradaEm is not null || j.Status != PartidaStatus.Agendada,
                    j.EncerradaEm is not null || j.Status == PartidaStatus.Encerrada,
                    j.GolsCasa, j.GolsFora, j.IsWO,
                    RegrasNovas(r.DataHora ?? j.EncerradaEm),
                    j.DataHora ?? r.DataHora,
                    PrecisaDeVencedor(r.Tipo, r.Numero, r.Desempate, doMataMata.Contains(j.PartidaId)),
                    j.TemPenaltis ? j.PenaltisVencedorId : null,
                    j.EncerradaEm is DateTime fim
                        ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fim, DateTimeKind.Utc), HorarioDeBrasilia.Fuso)
                        : null))
                    .OrderBy(j => j.Quando ?? DateTime.MaxValue)
                    .ToList()))
            .ToList();

        return _cache;
    }

    /// <summary>
    /// Jogo único que precisa de vencedor, igual à importação do PES: Supercopa, mata-mata, jogos decisivos
    /// e playoff de acesso (rodada de desempate). A mini liga é por pontos e aceita empate.
    /// </summary>
    private static bool PrecisaDeVencedor(TipoCompetition tipo, int numero, bool desempate, bool doMataMata) =>
        tipo == TipoCompetition.Supercopa || doMataMata || desempate || numero == -1;

    private static string? NomeDoTime(Jogo jogo, Guid? timeId) =>
        timeId == jogo.CasaId ? jogo.Casa : timeId == jogo.ForaId ? jogo.Fora : null;

    /// <summary>Jogo sem data nem encerramento ainda está por vir, então já é das regras novas.</summary>
    private static bool RegrasNovas(DateTime? quando) =>
        quando is not DateTime dia || dia >= BolaoPontuacao.RegrasNovasDesde;

    /// <summary>
    /// O jogo aceita palpite enquanto a bola não rolar: antes do horário dele e sem ter começado.
    /// Jogo sem data fica aberto até o apito.
    /// </summary>
    private bool JogoAberto(Jogo jogo) =>
        !jogo.Comecou && (jogo.Quando is not DateTime quando || Agora < quando);

    /// <summary>
    /// A rodada aceita palpite enquanto tiver jogo por vir: as estreias saem uma depois da outra,
    /// e quem chega no meio da rodada ainda palpita nos jogos que faltam.
    /// </summary>
    private bool EstaAberta(Rodada rodada) => rodada.Jogos.Any(JogoAberto);

    private async Task<List<Palpite>> PalpitesAsync(IEnumerable<Guid> partidaIds, Guid? treinadorId, CancellationToken ct)
    {
        var ids = partidaIds.ToList();

        return await _db.BolaoPalpites.AsNoTracking()
            .Where(p => ids.Contains(p.PartidaId) && (treinadorId == null || p.TreinadorId == treinadorId))
            .Select(p => new Palpite(
                p.TreinadorId, p.Treinador.Nome, p.PartidaId, p.GolsCasa, p.GolsFora,
                p.JogadorCasaId, p.JogadorCasa != null ? p.JogadorCasa.Name : null,
                p.JogadorForaId, p.JogadorFora != null ? p.JogadorFora.Name : null,
                p.PenaltisVencedorId))
            .ToListAsync(ct);
    }

    /// <summary>Os lances dos jogos encerrados que têm aposta extra (guardados para as próximas contas).</summary>
    private async Task<IReadOnlyDictionary<Guid, Lances>> LancesAsync(IEnumerable<Jogo> jogos, CancellationToken ct)
    {
        var faltam = jogos
            .Where(j => j.Encerrado && j.RegrasNovas && !_lances.ContainsKey(j.PartidaId))
            .Select(j => j.PartidaId)
            .Distinct()
            .ToList();

        if (faltam.Count > 0)
        {
            var eventos = await _db.LigaEventos.AsNoTracking()
                .Where(e => faltam.Contains(e.PartidaId) && (e.Tipo == TipoEvento.Gol || e.Tipo == TipoEvento.Substituicao))
                .Select(e => new { e.PartidaId, e.Tipo, e.JogadorId, e.AssistenteId })
                .ToListAsync(ct);

            var escalacoes = await _db.LigaEscalacoes.AsNoTracking()
                .Where(e => faltam.Contains(e.PartidaId))
                .Select(e => new { e.PartidaId, e.TimeId, e.JogadorId, e.Titular })
                .ToListAsync(ct);

            foreach (var id in faltam)
            {
                var gols = eventos.Where(e => e.PartidaId == id && e.Tipo == TipoEvento.Gol).ToList();
                var entraram = eventos.Where(e => e.PartidaId == id && e.Tipo == TipoEvento.Substituicao).Select(e => e.JogadorId);
                var escalados = escalacoes.Where(e => e.PartidaId == id).ToList();

                _lances[id] = new Lances(
                    gols.Select(e => e.JogadorId).Concat(gols.Select(e => e.AssistenteId).OfType<int>()).ToHashSet(),
                    escalados.Where(e => e.Titular).Select(e => e.JogadorId).Concat(entraram).ToHashSet(),
                    escalados.Select(e => e.TimeId).ToHashSet());
            }
        }

        return _lances;
    }

    private static Apuracao Apurar(Palpite p, Jogo jogo, IReadOnlyDictionary<Guid, Lances> lances)
    {
        var placar = BolaoPontuacao.Calcular(p.GolsCasa, p.GolsFora, jogo.GolsCasa, jogo.GolsFora, jogo.RegrasNovas);
        var penaltis = BolaoPontuacao.Penaltis(p.PenaltisId, jogo.PenaltisVencedorId is not null, jogo.PenaltisVencedorId);

        // W.O. não tem lance: só o placar conta.
        if (!jogo.RegrasNovas || jogo.IsWO) return new(placar, null, null, penaltis);

        var doJogo = lances.GetValueOrDefault(jogo.PartidaId) ?? Lances.Vazio;
        return new(placar,
            Lado(p.GolsCasa, jogo.GolsCasa, p.JogadorCasaId, jogo.CasaId, doJogo),
            Lado(p.GolsFora, jogo.GolsFora, p.JogadorForaId, jogo.ForaId, doJogo),
            penaltis);
    }

    /// <summary>
    /// A aposta extra de um lado. Sem retrato da escalação do time não dá para saber quem jogou,
    /// então o jogador escolhido conta como se tivesse entrado.
    /// </summary>
    private static int? Lado(int palpite, int gols, int? jogadorId, Guid timeId, Lances lances)
    {
        var participou = jogadorId is int id && lances.ComGA.Contains(id);
        var entrou = jogadorId is int j
            && (participou || lances.EmCampo.Contains(j) || !lances.TimesComEscalacao.Contains(timeId));

        return BolaoPontuacao.Extra(palpite, gols, jogadorId is not null, participou, entrou);
    }

    /// <summary>Atacantes primeiro: é de onde sai a maioria dos gols e assistências.</summary>
    private static int OrdemNoAtaque(short posicao) => (PositionType)posicao switch
    {
        PositionType.Centroavante => 0,
        PositionType.SegundoAtacante => 1,
        PositionType.PontaEsquerda or PositionType.PontaDireita => 2,
        PositionType.MeiaAtacante => 3,
        PositionType.MeiaEsquerda or PositionType.MeiaDireita => 4,
        PositionType.MeiaLigacao => 5,
        PositionType.Volante => 6,
        PositionType.LateralEsquerdo or PositionType.LateralDireito => 7,
        PositionType.Zagueiro => 8,
        _ => 9
    };

    private async Task<IReadOnlyList<BolaoRodadaDto>> MontarAsync(
        IEnumerable<Rodada> rodadas, Guid? treinadorId, CancellationToken ct)
    {
        var lista = rodadas.ToList();
        if (lista.Count == 0) return Array.Empty<BolaoRodadaDto>();

        var meus = new Dictionary<Guid, Palpite>();
        var lances = (IReadOnlyDictionary<Guid, Lances>)_lances;
        if (treinadorId is Guid id)
        {
            var partidas = lista.SelectMany(r => r.Jogos.Select(j => j.PartidaId));
            meus = (await PalpitesAsync(partidas, id, ct)).ToDictionary(p => p.PartidaId);
            lances = await LancesAsync(lista.SelectMany(r => r.Jogos).Where(j => meus.ContainsKey(j.PartidaId)), ct);
        }

        return lista.Select(r => new BolaoRodadaDto(
            r.RodadaId, r.LigaId, r.Competicao, r.Tipo, r.Numero, Titulo(r), r.Quando, EstaAberta(r),
            r.Jogos.Select(j =>
            {
                var meu = meus.GetValueOrDefault(j.PartidaId);
                var apurado = meu is not null && j.Encerrado ? Apurar(meu, j, lances) : null;

                return new BolaoJogoDto(
                    j.PartidaId, j.CasaId, j.Casa, j.ForaId, j.Fora,
                    j.Encerrado,
                    j.Encerrado ? j.GolsCasa : null,
                    j.Encerrado ? j.GolsFora : null,
                    meu?.GolsCasa,
                    meu?.GolsFora,
                    apurado?.Total,
                    j.RegrasNovas,
                    meu?.JogadorCasaId, meu?.JogadorCasa,
                    meu?.JogadorForaId, meu?.JogadorFora,
                    apurado?.Placar, apurado?.ExtraCasa, apurado?.ExtraFora,
                    JogoAberto(j), j.Quando,
                    j.Eliminatorio, meu?.PenaltisId, apurado?.Penaltis,
                    j.Encerrado ? j.PenaltisVencedorId : null);
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

        var palpites = await PalpitesAsync(jogos.Keys, null, ct);
        var lances = await LancesAsync(jogos.Values, ct);
        var clubes = await ClubesAtuaisAsync(ct);

        var linhas = palpites
            .GroupBy(p => new { p.TreinadorId, p.Nome })
            .Select(g =>
            {
                var apurados = g.Select(p => Apurar(p, jogos[p.PartidaId], lances)).ToList();

                return new
                {
                    g.Key.TreinadorId,
                    g.Key.Nome,
                    Pontos = apurados.Sum(a => a.Total),
                    Palpites = apurados.Count,
                    Cravadas = apurados.Count(a => a.Placar == BolaoPontuacao.PlacarExato),
                    Acertos = apurados.Count(a => a.Placar > 0)
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
