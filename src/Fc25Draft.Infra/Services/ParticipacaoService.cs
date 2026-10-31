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

    /// <param name="PapeisParaGravar">Quem chegou depois que o papel entrou no ar e ainda não tem o papel gravado.</param>
    private sealed record Montagem(
        DateTime CalculadaEm,
        IReadOnlyDictionary<int, ParticipacaoJogador> Jogadores,
        IReadOnlyDictionary<int, Entrada> Entradas,
        IReadOnlyDictionary<int, string> Nomes,
        IReadOnlyDictionary<Guid, string> Times,
        IReadOnlyDictionary<int, (Guid TimeId, PapelNoElenco Papel)> PapeisParaGravar);

    /// <summary>O que entrou na conta de cada jogador (para refazer jogo a jogo).</summary>
    private sealed record Entrada(Guid TimeId, IReadOnlyList<JogoDoJogador> Jogos, bool Goleiro, bool TemNivel, decimal? VariacaoHerdada,
        Expectativa? Papel);

    /// <summary>Quando e como o jogador chegou ao clube atual.</summary>
    private sealed record Chegada(DateTime Quando, decimal? VariacaoEmBaixa, ChegadaAoClube Origem);

    /// <summary>Esquece a conta guardada: quem pediu para sair ou recebeu promessa aparece na hora.</summary>
    public static void Esquecer()
    {
        lock (Trava) _guardada = null;
    }

    public async Task<IReadOnlyDictionary<int, ParticipacaoJogador>> GetTodosAsync(CancellationToken ct) =>
        (await MontarAsync(ct)).Jogadores;

    public async Task<ParticipacaoJogador?> GetAsync(int playerId, CancellationToken ct) =>
        (await MontarAsync(ct)).Jogadores.GetValueOrDefault(playerId);

    public async Task<IReadOnlyList<PontoDeParticipacao>> GetHistoricoAsync(int playerId, CancellationToken ct) =>
        (await MontarAsync(ct)).Entradas.GetValueOrDefault(playerId) is { } e
            ? Participacao.Historico(e.TimeId, e.Jogos, e.Goleiro, e.TemNivel, e.VariacaoHerdada, e.Papel)
            : [];

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
            .Select(r => new
            {
                r.TeamId, r.PlayerId, r.Player.Name, r.Player.Overall, r.Player.PositionId,
                r.SaidaPedidaEm, r.VariacaoCongelada, r.PromessaEm, r.PromessaQuebrada, r.Papel, r.ConversaEm, r.Player.Age
            })
            .ToListAsync(ct);
        var times = await _db.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);

        // Jogos do clube = jogos encerrados em que o clube tem escalação gravada.
        var titulares = await _db.LigaEscalacoes.AsNoTracking()
            .Where(e => e.Titular && e.Partida.Status == PartidaStatus.Encerrada)
            .Select(e => new
            {
                e.PartidaId, e.TimeId, e.JogadorId, e.Jogador.PositionId,
                Data = e.Partida.EncerradaEm ?? e.Partida.Rodada.DataHora
            })
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

        var chegadas = await ChegadasAsync(ct);

        // Pedir para sair: emprestado não pede; os jogos contam desde que a regra entrou no ar (ou desde a virada).
        var emprestados = (await _db.Emprestimos.AsNoTracking()
                .Where(e => e.Status == EmprestimoStatus.Ativo)
                .Select(e => e.PlayerId)
                .ToListAsync(ct))
            .ToHashSet();
        var config = await _db.TransferConfigs.AsNoTracking()
            .Select(c => new { c.PedidoDeSaidaDesde, c.PapelNoElencoDesde })
            .FirstOrDefaultAsync(ct);
        var pedidoDesde = config?.PedidoDeSaidaDesde;
        // A conta do passado (antes de o papel entrar no ar) é a da regra antiga.
        var papelDesde = config?.PapelNoElencoDesde is DateTime d && (ate is null || ate >= d) ? d : (DateTime?)null;

        // "Tem nível" da regra antiga: overall perto da média dos 11 melhores do elenco.
        var mediaDoXi = elenco.GroupBy(e => e.TeamId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Overall).OrderByDescending(o => o).Take(11).DefaultIfEmpty(0).Average());

        // Regra do papel: quantos titulares cada grupo de posição tem no time (pelas escalações recentes) e o início
        // de cada temporada (para a prata da casa).
        var titularesPorGrupo = papelDesde is null
            ? new Dictionary<Guid, IReadOnlyDictionary<GrupoDePosicao, int>>()
            : titulares.GroupBy(t => t.TimeId).ToDictionary(g => g.Key, g => TitularesPorGrupo(
                g.GroupBy(t => t.PartidaId)
                    .OrderByDescending(p => p.First().Data)
                    .Take(PapelCriterios.JogosParaContarTitulares)
                    .Select(p => p.Select(t => PapelDoJogador.Grupo(t.PositionId)).ToList())
                    .ToList()));
        var temporadasEncerradas = papelDesde is null ? [] : await InicioDasTemporadasEncerradasAsync(ct);
        var porGrupo = elenco.ToLookup(e => (e.TeamId, PapelDoJogador.Grupo(e.PositionId)));

        var jogadores = new Dictionary<int, ParticipacaoJogador>(elenco.Count);
        var entradas = new Dictionary<int, Entrada>(elenco.Count);
        var paraGravar = new Dictionary<int, (Guid, PapelNoElenco)>();
        foreach (var j in elenco)
        {
            var chegada = chegadas.GetValueOrDefault((j.PlayerId, j.TeamId));
            var desde = chegada?.Quando;
            var jogos = (jogosDoTime.GetValueOrDefault(j.TeamId) ?? [])
                .Where(x => (desde is null || x.Data >= desde) && (ate is null || x.Data <= ate)
                            && !suspensoEm.Contains((x.PartidaId, j.PlayerId)))
                .Select(x => new JogoDoJogador(x.PartidaId, x.Data,
                    titularEm.Contains((x.PartidaId, j.PlayerId)), entrouEm.Contains((x.PartidaId, j.PlayerId))))
                .ToList();

            var goleiro = j.PositionId == (short)PositionType.Goleiro;
            var temNivel = j.Overall >= mediaDoXi[j.TeamId] - ParticipacaoCriterios.MargemDoNivel;
            var herdada = chegada?.VariacaoEmBaixa;

            Expectativa? papel = null;
            if (papelDesde is DateTime valeDesde)
            {
                var grupo = PapelDoJogador.Grupo(j.PositionId);
                var vagas = (titularesPorGrupo.GetValueOrDefault(j.TeamId) ?? PapelCriterios.TitularesPadrao)[grupo];
                var outros = porGrupo[(j.TeamId, grupo)].Where(o => o.PlayerId != j.PlayerId).Select(o => o.Overall).ToList();
                var entreOsMelhores = PapelDoJogador.EntreOsMelhores(j.Overall, outros, vagas);

                // Sem papel gravado: quem chegou depois da regra tem o da chegada (e grava); os de antes (a virada
                // grava todos), pela posição.
                var tipo = j.Papel ?? (entreOsMelhores ? PapelNoElenco.Disputa : PapelNoElenco.Rotacao);
                if (j.Papel is null && chegada is not null && chegada.Quando >= valeDesde)
                {
                    tipo = PapelDoJogador.PelaChegada(chegada.Origem, entreOsMelhores, j.Age);
                    paraGravar[j.PlayerId] = (j.TeamId, tipo);
                }

                var prataDaCasa = TemporadasCompletas(desde, temporadasEncerradas) >= PapelCriterios.TemporadasDaPrataDaCasa;
                papel = new Expectativa(tipo, entreOsMelhores,
                    prataDaCasa ? PapelCriterios.JanelaDaPrataDaCasa : ParticipacaoCriterios.JanelaDoInsatisfeito,
                    valeDesde, j.ConversaEm, prataDaCasa);
            }

            // O que está travado na lista vale hoje; a conta do passado não tem lista nem promessa.
            if (ate is not null)
            {
                jogadores[j.PlayerId] = Participacao.Calcular(j.TeamId, jogos, goleiro, temNivel, herdada, papel: papel);
            }
            else
            {
                var travada = j.SaidaPedidaEm is not null ? j.VariacaoCongelada : null;
                var p = Participacao.Calcular(j.TeamId, jogos, goleiro, temNivel, herdada, travada, papel);
                int? jogosInsatisfeito = p.InsatisfeitoDesde is { } h && pedidoDesde is DateTime contaDesde
                                         && !goleiro && !emprestados.Contains(j.PlayerId)
                    ? PedidoDeSaida.JogosInsatisfeito(jogos, h, contaDesde)
                    : null;
                jogadores[j.PlayerId] = p with
                {
                    Saida = new SituacaoDeSaida(
                        j.SaidaPedidaEm,
                        jogosInsatisfeito,
                        j.PromessaEm,
                        j.PromessaEm is DateTime prometeu ? PedidoDeSaida.JogosDepois(jogos, prometeu) : 0,
                        j.PromessaQuebrada)
                };
            }
            entradas[j.PlayerId] = new Entrada(j.TeamId, jogos, goleiro, temNivel, herdada, papel);
        }

        return new Montagem(agora, jogadores, entradas, elenco.ToDictionary(e => e.PlayerId, e => e.Name), times, paraGravar);
    }

    /// <summary>
    /// Quando cada um chegou ao clube atual e como: transferência ou escolha no draft, a mais recente. Quem saiu do
    /// clube anterior depois de pedir para sair chega com o preço em baixa.
    /// </summary>
    private async Task<Dictionary<(int PlayerId, Guid TimeId), Chegada>> ChegadasAsync(CancellationToken ct)
    {
        // Leilão de livres: quanto pagou sobre o lance inicial (e se foi no comprar já).
        var leiloes = (await _db.MarketItems.AsNoTracking()
                .Where(i => i.Status == MarketItemStatus.Sold && i.WinnerTeamId != null && i.BasePrice > 0)
                .Select(i => new { i.CycleId, i.PlayerId, i.BasePrice, i.BuyNowPrice })
                .ToListAsync(ct))
            .GroupBy(i => (i.CycleId, i.PlayerId))
            .ToDictionary(g => g.Key, g => g.First());

        var transferencias = (await _db.TransferHistories.AsNoTracking()
                .Where(t => t.ToTeamId != null)
                .Select(t => new { t.PlayerId, TimeId = t.ToTeamId!.Value, t.PerformedAtUtc, t.VariacaoEmBaixa, t.Type, t.CycleId, t.Amount })
                .ToListAsync(ct))
            .Select(t =>
            {
                var leilao = t.Type == TransferType.MarketAuction && t.CycleId is Guid ciclo && t.Amount is not null
                    ? leiloes.GetValueOrDefault((ciclo, t.PlayerId))
                    : null;
                var origem = leilao is null
                    ? new ChegadaAoClube(Expansao: t.Type == TransferType.ExpansionDraft)
                    : new ChegadaAoClube(
                        Leilao: t.Amount!.Value / leilao.BasePrice,
                        CompraImediata: leilao.BuyNowPrice is decimal imediata && t.Amount >= imediata);
                return (t.PlayerId, t.TimeId, Chegada: new Chegada(t.PerformedAtUtc, t.VariacaoEmBaixa, origem));
            });

        var escolhas = (await _db.DraftPicks.AsNoTracking()
                .Where(p => p.PlayerId != null && p.PickedAtUtc != null)
                .Select(p => new
                {
                    PlayerId = p.PlayerId!.Value, p.TeamId, PickedAtUtc = p.PickedAtUtc!.Value,
                    p.RoundNumber, p.PickInRound, p.Draft.TotalTeams, p.Draft.Tipo
                })
                .ToListAsync(ct))
            .Select(p => (p.PlayerId, TimeId: p.TeamId, Chegada: new Chegada(p.PickedAtUtc, null,
                new ChegadaAoClube(DraftRodada: p.RoundNumber, DraftPick: p.PickInRound, DraftTimes: p.TotalTeams,
                    Expansao: p.Tipo == DraftTipo.Expansao))));

        return transferencias.Concat(escolhas)
            .GroupBy(c => (c.PlayerId, c.TimeId))
            .ToDictionary(g => g.Key, g => g
                .OrderByDescending(c => c.Chegada.Quando)
                // Na expansão a escolha e a transferência saem juntas: a escolha diz a rodada.
                .ThenBy(c => c.Chegada.Origem.DraftRodada is null)
                .First().Chegada);
    }

    /// <summary>Titulares de cada grupo de posição: a média das escalações recentes (sem escalação, o padrão).</summary>
    private static IReadOnlyDictionary<GrupoDePosicao, int> TitularesPorGrupo(IReadOnlyList<List<GrupoDePosicao>> escalacoes)
    {
        if (escalacoes.Count == 0) return PapelCriterios.TitularesPadrao;
        return Enum.GetValues<GrupoDePosicao>().ToDictionary(g => g,
            g => (int)Math.Round(escalacoes.Average(e => e.Count(x => x == g)), MidpointRounding.AwayFromZero));
    }

    /// <summary>Início das temporadas que já acabaram (pela Série A).</summary>
    private async Task<List<DateTime>> InicioDasTemporadasEncerradasAsync(CancellationToken ct) =>
        await _db.Ligas.AsNoTracking()
            .Where(l => l.Tipo == TipoCompetition.Liga && (l.Divisao == null || l.Divisao == Divisao.SerieA)
                        && l.Status == LigaStatus.Encerrada)
            .Select(l => l.DataInicio)
            .ToListAsync(ct);

    /// <summary>Temporadas inteiras no clube: as encerradas que começaram depois que ele chegou (com tolerância).</summary>
    private static int TemporadasCompletas(DateTime? chegouEm, IReadOnlyList<DateTime> inicios) =>
        chegouEm is not DateTime chegou
            ? inicios.Count
            : inicios.Count(inicio => chegou <= inicio.AddDays(PapelCriterios.ToleranciaDoInicioEmDias));

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
                        $"Jogou {h.PercentualNaJanela * 100:0}% dos últimos {p.JanelaDoInsatisfeito} jogos do clube · valor de mercado em queda",
                        link, time, playerId)
                    : new PlantaoNoticiaDto(h.Data.AddSeconds(2), PlantaoCategoria.Mercado, "🤝",
                        $"{nome} volta a ser titular e faz as pazes com o {time}",
                        $"{ParticipacaoCriterios.TitularSeguidoParaResolver} jogos seguidos como titular", link, time, playerId));
            }

            if (p.Saida?.SaidaPedidaEm is DateTime pediu)
                noticias.Add(new PlantaoNoticiaDto(pediu, PlantaoCategoria.Mercado, "🚪",
                    p.Saida.PromessaQuebrada
                        ? $"Promessa não cumprida: {nome} volta à lista do {time} pela metade do preço"
                        : Escolher(p.TimeId, playerId,
                            $"{nome} pede para sair do {time} e vai para a lista de transferências",
                            $"Cansado do banco, {nome} força a saída do {time}",
                            $"{nome} bate o pé e é colocado à venda pelo {time}"),
                    p.Saida.PromessaQuebrada
                        ? "Preço em baixa: metade da venda rápida, para quem quiser comprar"
                        : "Na lista pelo preço da venda rápida, para quem quiser comprar",
                    link, time, playerId));
            else if (p.Saida?.PromessaEm is DateTime prometeu)
                noticias.Add(new PlantaoNoticiaDto(prometeu, PlantaoCategoria.Mercado, "🤝",
                    $"O {time} promete colocar {nome} para jogar e tira ele da lista",
                    $"Tem {PedidoDeSaidaCriterios.JogosDaPromessa} jogos para voltar a ser titular "
                    + $"{ParticipacaoCriterios.TitularSeguidoParaResolver} jogos seguidos",
                    link, time, playerId));
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
                $"😤 {nome} está insatisfeito: jogou {h.PercentualNaJanela * 100:0}% dos últimos "
                + $"{p.JanelaDoInsatisfeito} jogos. O valor de mercado dele cai mais 10% até ele ser titular "
                + $"{ParticipacaoCriterios.TitularSeguidoParaResolver} jogos seguidos. Se seguir insatisfeito por mais "
                + $"{PedidoDeSaidaCriterios.JogosAtePedir} jogos, pede para sair e vai sozinho para a lista pelo preço da venda rápida.",
                $"/players/details/{playerId}", agora);
            avisados++;
        }

        if (avisados > 0) await _db.SaveChangesAsync(ct);
        return avisados;
    }

    public async Task<int> AvisarIncomodadosAsync(CancellationToken ct)
    {
        var m = await MontarAsync(ct);
        var agora = _time.GetUtcNow().UtcDateTime;
        var avisados = 0;

        foreach (var (playerId, p) in m.Jogadores)
        {
            // Um aviso por jogo: o último jogo do clube que deixou ele assim, se for recente.
            if (!p.Incomodado || p.Papel is not { } papel || m.Entradas.GetValueOrDefault(playerId)?.Jogos is not [.., var ultimo]
                || ultimo.Data < agora - AvisoVale)
                continue;

            var chave = $"incomodado:{playerId}:{ultimo.PartidaId}";
            if (await _db.NotificacoesEnviadas.AnyAsync(n => n.Chave == chave, ct)) continue;
            _db.NotificacoesEnviadas.Add(new NotificacaoEnviada { Chave = chave, EnviadaEm = agora });

            var nome = m.Nomes.GetValueOrDefault(playerId, "?");
            AvisosDoTime.Criar(_db, p.TimeId, AvisosDoTime.Incomodado,
                $"😐 {nome} está incomodado ({PapelDoJogador.Emoji(papel.Papel)} {PapelDoJogador.Nome(papel.Papel)}): se não "
                + "jogar o próximo jogo, fica 😤 insatisfeito e o valor de mercado dele cai mais 10%.",
                $"/players/details/{playerId}", agora);
            avisados++;
        }

        if (avisados > 0) await _db.SaveChangesAsync(ct);
        return avisados;
    }

    public async Task<int> GravarPapeisAsync(CancellationToken ct)
    {
        // Roda a cada minuto: o elenco da conta (de até um minuto atrás) é o que ele encontrou ao chegar.
        var m = await MontarAsync(ct);
        if (m.PapeisParaGravar.Count == 0) return 0;

        var ids = m.PapeisParaGravar.Keys.ToList();
        var vinculos = await _db.TeamRosters.Where(r => ids.Contains(r.PlayerId) && r.Papel == null).ToListAsync(ct);
        foreach (var v in vinculos)
            if (m.PapeisParaGravar.TryGetValue(v.PlayerId, out var p) && p.TimeId == v.TeamId)
                v.Papel = p.Papel;

        await _db.SaveChangesAsync(ct);
        Esquecer();
        return vinculos.Count;
    }

    public async Task<IReadOnlyDictionary<int, PapelNoElenco>> PapeisNaViradaAsync(int temporada, CancellationToken ct)
    {
        Esquecer();
        var m = await MontarAsync(ct);
        var daTemporada = (await _db.LigaPartidas.AsNoTracking()
                .Where(p => p.Rodada.Liga.Temporada == temporada)
                .Select(p => p.PartidaId)
                .ToListAsync(ct))
            .ToHashSet();
        var idades = await _db.Players.AsNoTracking()
            .Where(p => p.CurrentTeamId != null)
            .ToDictionaryAsync(p => p.PlayerId, p => p.Age, ct);

        var papeis = new Dictionary<int, PapelNoElenco>(m.Jogadores.Count);
        foreach (var (playerId, p) in m.Jogadores)
        {
            if (p.Papel is not { } atual) continue;
            // O que jogou pelo clube atual na temporada que acabou (pouco jogo = não dá para dizer).
            var jogos = m.Entradas.GetValueOrDefault(playerId)?.Jogos.Where(j => daTemporada.Contains(j.PartidaId)).ToList() ?? [];
            decimal? participacao = jogos.Count >= ParticipacaoCriterios.JogosParaValer ? jogos.Sum(j => j.Peso) / jogos.Count : null;
            papeis[playerId] = PapelDoJogador.NaVirada(participacao, atual.Papel, idades.GetValueOrDefault(playerId));
        }
        return papeis;
    }

    // A mesma manchete sempre para o mesmo jogador no mesmo jogo.
    private static string Escolher(Guid partidaId, int playerId, params string[] opcoes) =>
        opcoes[(int)((uint)(partidaId.GetHashCode() ^ playerId) % (uint)opcoes.Length)];
}
