using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Participação de cada jogador no clube atual, pelos jogos com escalação gravada (titulares) e pelas
/// substituições. A conta lê todos os jogos; fica guardada por um minuto para as telas que pedem o valor de
/// mercado jogador por jogador.
/// </summary>
public class ParticipacaoService : IParticipacaoService
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan AvisoVale = TimeSpan.FromDays(2);
    private static readonly object Trava = new();
    private static Montagem? _guardada;

    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public ParticipacaoService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    private sealed record Montagem(
        DateTime CalculadaEm,
        IReadOnlyDictionary<int, ParticipacaoJogador> Jogadores,
        IReadOnlyDictionary<int, string> Nomes,
        IReadOnlyDictionary<Guid, string> Times);

    public async Task<IReadOnlyDictionary<int, ParticipacaoJogador>> GetTodosAsync(CancellationToken ct) =>
        (await MontarAsync(ct)).Jogadores;

    public async Task<ParticipacaoJogador?> GetAsync(int playerId, CancellationToken ct) =>
        (await MontarAsync(ct)).Jogadores.GetValueOrDefault(playerId);

    public async Task<IReadOnlyDictionary<int, ParticipacaoJogador>> GetTodosEmAsync(DateTime ateUtc, CancellationToken ct) =>
        (await CalcularAsync(_time.GetUtcNow().UtcDateTime, ateUtc, ct)).Jogadores;

    private async Task<Montagem> MontarAsync(CancellationToken ct)
    {
        var agora = _time.GetUtcNow().UtcDateTime;
        lock (Trava)
        {
            if (_guardada is { } g && agora - g.CalculadaEm < Validade) return g;
        }

        var montagem = await CalcularAsync(agora, null, ct);
        lock (Trava) _guardada = montagem;
        return montagem;
    }

    /// <param name="ate">Só os jogos até essa data (como estava naquele dia, com o elenco de hoje); nulo = todos.</param>
    private async Task<Montagem> CalcularAsync(DateTime agora, DateTime? ate, CancellationToken ct)
    {
        var elenco = await _db.TeamRosters.AsNoTracking()
            .Select(r => new { r.TeamId, r.PlayerId, r.Player.Name, r.Player.Overall, r.Player.PositionId })
            .ToListAsync(ct);
        var times = await _db.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);

        // Jogos do clube = jogos encerrados em que o clube tem escalação gravada.
        var titulares = await _db.LigaEscalacoes.AsNoTracking()
            .Where(e => e.Titular && e.Partida.Status == PartidaStatus.Encerrada)
            .Select(e => new { e.PartidaId, e.TimeId, e.JogadorId, Data = e.Partida.EncerradaEm ?? e.Partida.Rodada.DataHora })
            .ToListAsync(ct);
        var jogosDoTime = titulares
            .GroupBy(t => t.TimeId)
            .ToDictionary(g => g.Key, g => g.Select(t => (t.PartidaId, Data: t.Data ?? DateTime.MinValue)).Distinct()
                .OrderBy(j => j.Data).ThenBy(j => j.PartidaId).ToList());
        var titularEm = titulares.Select(t => (t.PartidaId, t.JogadorId)).ToHashSet();

        var partidaIds = titulares.Select(t => t.PartidaId).Distinct().ToList();
        var entrouEm = (await _db.LigaEventos.AsNoTracking()
                .Where(e => e.Tipo == TipoEvento.Substituicao && partidaIds.Contains(e.PartidaId))
                .Select(e => new { e.PartidaId, e.JogadorId })
                .ToListAsync(ct))
            .Select(e => (e.PartidaId, e.JogadorId))
            .ToHashSet();

        var suspensoEm = await SuspensoesAsync(partidaIds, ct);

        // Quando cada um chegou ao clube atual: transferência ou escolha no draft, a mais recente.
        var chegadas = (await _db.TransferHistories.AsNoTracking()
                .Where(t => t.ToTeamId != null)
                .Select(t => new { t.PlayerId, TimeId = t.ToTeamId!.Value, Quando = t.PerformedAtUtc })
                .ToListAsync(ct))
            .Concat((await _db.DraftPicks.AsNoTracking()
                    .Where(p => p.PlayerId != null && p.PickedAtUtc != null)
                    .Select(p => new { PlayerId = p.PlayerId!.Value, TimeId = p.TeamId, Quando = p.PickedAtUtc!.Value })
                    .ToListAsync(ct)))
            .GroupBy(c => (c.PlayerId, c.TimeId))
            .ToDictionary(g => g.Key, g => g.Max(c => c.Quando));

        // "Tem nível": overall perto da média dos 11 melhores do elenco.
        var mediaDoXi = elenco.GroupBy(e => e.TeamId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Overall).OrderByDescending(o => o).Take(11).DefaultIfEmpty(0).Average());

        var jogadores = new Dictionary<int, ParticipacaoJogador>(elenco.Count);
        foreach (var j in elenco)
        {
            var desde = chegadas.TryGetValue((j.PlayerId, j.TeamId), out var c) ? c : (DateTime?)null;
            var jogos = (jogosDoTime.GetValueOrDefault(j.TeamId) ?? [])
                .Where(x => (desde is null || x.Data >= desde) && (ate is null || x.Data <= ate)
                            && !suspensoEm.Contains((x.PartidaId, j.PlayerId)))
                .Select(x => new JogoDoJogador(x.PartidaId, x.Data,
                    titularEm.Contains((x.PartidaId, j.PlayerId)), entrouEm.Contains((x.PartidaId, j.PlayerId))))
                .ToList();

            var goleiro = j.PositionId == (short)PositionType.Goleiro;
            var temNivel = j.Overall >= mediaDoXi[j.TeamId] - ParticipacaoCriterios.MargemDoNivel;
            jogadores[j.PlayerId] = Participacao.Calcular(j.TeamId, jogos, goleiro, temNivel);
        }

        return new Montagem(agora, jogadores, elenco.ToDictionary(e => e.PlayerId, e => e.Name), times);
    }

    /// <summary>(partida, jogador) em que o jogador cumpria suspensão: esse jogo não conta para ele.</summary>
    private async Task<HashSet<(Guid PartidaId, int JogadorId)>> SuspensoesAsync(List<Guid> partidaIds, CancellationToken ct)
    {
        var ligas = await _db.LigaPartidas.AsNoTracking()
            .Where(p => partidaIds.Contains(p.PartidaId)
                        && (p.Rodada.Liga.Tipo == TipoCompetition.Liga || p.Rodada.Liga.Tipo == TipoCompetition.Copa))
            .Select(p => p.Rodada.LigaId)
            .Distinct()
            .ToListAsync(ct);

        var suspensoes = new HashSet<(Guid, int)>();
        foreach (var ligaId in ligas)
        {
            var disciplina = await DisciplinaDaCompeticao.CalcularAsync(_db, ligaId, ct);
            foreach (var s in disciplina?.Suspensoes ?? [])
                if (s.PartidaCumprida is Guid partida)
                    suspensoes.Add((partida, s.JogadorId));
        }
        return suspensoes;
    }

    public async Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct)
    {
        var m = await MontarAsync(ct);
        var noticias = new List<PlantaoNoticiaDto>();
        foreach (var (playerId, p) in m.Jogadores)
        {
            var nome = m.Nomes.GetValueOrDefault(playerId, "?");
            var time = m.Times.GetValueOrDefault(p.TimeId);
            var link = $"/players/details/{playerId}";
            foreach (var h in p.Humor)
            {
                noticias.Add(h.Insatisfeito
                    ? new PlantaoNoticiaDto(h.Data.AddSeconds(2), PlantaoCategoria.Mercado, "😤",
                        Escolher(h.PartidaId, playerId,
                            $"{nome} se irrita com o banco e pede para sair do {time}",
                            $"Insatisfeito, {nome} quer deixar o {time}",
                            $"{nome} perde a paciência no {time} e pede para ser negociado"),
                        $"Jogou {h.PercentualNaJanela * 100:0}% dos últimos {ParticipacaoCriterios.JanelaDoInsatisfeito} jogos do clube · valor de mercado em queda",
                        link, time, playerId)
                    : new PlantaoNoticiaDto(h.Data.AddSeconds(2), PlantaoCategoria.Mercado, "🤝",
                        $"{nome} volta a ser titular e faz as pazes com o {time}",
                        $"{ParticipacaoCriterios.TitularSeguidoParaResolver} jogos seguidos como titular", link, time, playerId));
            }
        }
        return noticias.OrderByDescending(n => n.Data).ToArray();
    }

    public async Task<int> AvisarInsatisfeitosAsync(CancellationToken ct)
    {
        var m = await MontarAsync(ct);
        var agora = _time.GetUtcNow().UtcDateTime;
        var avisados = 0;

        foreach (var (playerId, p) in m.Jogadores)
        {
            // Só quem ficou insatisfeito num jogo recente (na primeira checagem, os antigos não viram aviso).
            if (p.InsatisfeitoDesde is not { } h || h.Data < agora - AvisoVale) continue;

            var chave = $"insatisfeito:{playerId}:{h.PartidaId}";
            if (await _db.NotificacoesEnviadas.AnyAsync(n => n.Chave == chave, ct)) continue;
            _db.NotificacoesEnviadas.Add(new NotificacaoEnviada { Chave = chave, EnviadaEm = agora });

            var nome = m.Nomes.GetValueOrDefault(playerId, "?");
            AvisosDoTime.Criar(_db, p.TimeId, AvisosDoTime.Insatisfeito,
                $"😤 {nome} está insatisfeito e pediu para sair: jogou {h.PercentualNaJanela * 100:0}% dos últimos "
                + $"{ParticipacaoCriterios.JanelaDoInsatisfeito} jogos. O valor de mercado dele cai mais 10% até ele ser titular "
                + $"{ParticipacaoCriterios.TitularSeguidoParaResolver} jogos seguidos (ou sair do clube).",
                $"/players/details/{playerId}", agora);
            avisados++;
        }

        if (avisados > 0) await _db.SaveChangesAsync(ct);
        return avisados;
    }

    // A mesma manchete sempre para o mesmo jogador no mesmo jogo.
    private static string Escolher(Guid partidaId, int playerId, params string[] opcoes) =>
        opcoes[(int)((uint)(partidaId.GetHashCode() ^ playerId) % (uint)opcoes.Length)];
}
