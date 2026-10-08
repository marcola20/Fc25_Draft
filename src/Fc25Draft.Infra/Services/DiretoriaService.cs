using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Diretoria do clube: metas gravadas por temporada; confiança, situação das metas e notícias
/// calculadas na hora a partir dos jogos (nada disso é gravado).
/// </summary>
public class DiretoriaService : IDiretoriaService
{
    private const string OrigemLedger = "DIRETORIA";

    /// <summary>Rodada do playoff de acesso dentro da Série A (ver LigaTemporadaService).</summary>
    private const int NumeroPlayoffAcesso = -2;

    private readonly DraftDbContext _db;
    private readonly ITreinadorService? _treinadores;
    private readonly TimeProvider _time;

    public DiretoriaService(DraftDbContext db, ITreinadorService? treinadores = null, TimeProvider? time = null)
    {
        _db = db;
        _treinadores = treinadores;
        _time = time ?? TimeProvider.System;
    }

    public async Task<int?> GetTemporadaAtualAsync(CancellationToken ct) =>
        await _db.Ligas.AsNoTracking()
            .Where(l => l.Temporada != null && l.Tipo == TipoCompetition.Liga)
            .MaxAsync(l => l.Temporada, ct);

    public async Task<DiretoriaPainelDto?> GetPainelAsync(int? temporada, CancellationToken ct)
    {
        var t = temporada ?? await GetTemporadaAtualAsync(ct);
        return t is int ano ? (await MontarAsync(ano, ct)).Painel : null;
    }

    public async Task<DiretoriaTimeDto?> GetTimeAsync(Guid timeId, CancellationToken ct)
    {
        var painel = await GetPainelAsync(null, ct);
        return painel?.Times.FirstOrDefault(t => t.TimeId == timeId);
    }

    public async Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct)
    {
        // A diretoria passou a existir na primeira temporada com metas: antes dela não há notícia.
        var temporadas = await _db.MetasDiretoria.AsNoTracking()
            .Select(m => m.Temporada).Distinct().ToListAsync(ct);

        var noticias = new List<PlantaoNoticiaDto>();
        foreach (var t in temporadas)
        {
            var m = await MontarAsync(t, ct);
            noticias.AddRange(Diretoria.NoticiasDeConfianca(m.Pontos, m.Nomes));
            noticias.AddRange(NoticiasDeMetas(m));
            noticias.AddRange(NoticiasDeUltimatos(m));
        }

        return noticias.OrderByDescending(n => n.Data).ToArray();
    }

    // ── Avisos ─────────────────────────────────────────────────────────────

    public async Task<int> ProcessarJogosRecentesAsync(CancellationToken ct)
    {
        if (await GetTemporadaAtualAsync(ct) is not int temporada) return 0;

        var m = await MontarAsync(temporada, ct);
        var agora = _time.GetUtcNow().UtcDateTime;
        var desde = agora - AvisoDeFaixaVale;
        var avisados = 0;

        foreach (var (timeId, pontos) in m.Pontos)
        {
            for (int i = 0; i < pontos.Count; i++)
            {
                var ponto = pontos[i];
                // Jogo antigo (ex.: na primeira vez que a checagem roda) não vira aviso; ajuste da diretoria também não.
                if (ponto.Data < desde || ponto.Evento is not null) continue;

                var antes = Diretoria.Faixa(i > 0 ? pontos[i - 1].Valor : DiretoriaCriterios.ConfiancaInicial);
                var depois = Diretoria.Faixa(ponto.Valor);
                if (depois >= antes || depois > FaixaConfianca.Pressionado) continue;

                var chave = $"diretoria:{timeId}:{ponto.PartidaId}";
                if (await _db.NotificacoesEnviadas.AnyAsync(n => n.Chave == chave, ct)) continue;
                _db.NotificacoesEnviadas.Add(new NotificacaoEnviada { Chave = chave, EnviadaEm = agora });

                var jogo = $"o {ponto.GolsPro} x {ponto.GolsContra} contra o {m.Nomes.GetValueOrDefault(ponto.AdversarioId, "adversário")}";
                var texto = depois == FaixaConfianca.Pressionado
                    ? $"⚠️ A diretoria perdeu a paciência depois d{jogo}. Confiança: {ponto.Valor:0} de 100. Hora de reagir!"
                    : $"🔥 Sua cadeira está balançando: a diretoria entrou em crise depois d{jogo}. Confiança: {ponto.Valor:0} de 100.";

                AvisosDoTime.Criar(_db, timeId, AvisosDoTime.Diretoria, texto, $"/teams/details/{timeId}", agora);
                avisados++;
            }
        }

        avisados += await AvisarUltimatosAsync(m, desde, agora, ct);

        if (avisados > 0) await _db.SaveChangesAsync(ct);
        return avisados;
    }

    /// <summary>
    /// Ultimato novo: avisa o time. Ultimato fracassado: grava o pedido de demissão (para a organização decidir)
    /// e avisa o time em particular. Nada disso é salvo aqui: entra no SaveChanges de quem chamou.
    /// </summary>
    private async Task<int> AvisarUltimatosAsync(Montagem m, DateTime desde, DateTime agora, CancellationToken ct)
    {
        var avisados = 0;
        var pedidosGravados = m.Pedidos.Select(p => (p.TimeId, p.PartidaFalhaId)).ToHashSet();

        foreach (var (timeId, ultimatos) in m.Ultimatos)
        {
            foreach (var u in ultimatos)
            {
                if (u.Inicio >= desde)
                {
                    var chave = $"ultimato:{timeId}:{u.PartidaOrigemId}";
                    if (!await _db.NotificacoesEnviadas.AnyAsync(n => n.Chave == chave, ct))
                    {
                        _db.NotificacoesEnviadas.Add(new NotificacaoEnviada { Chave = chave, EnviadaEm = agora });
                        AvisosDoTime.Criar(_db, timeId, AvisosDoTime.Diretoria,
                            $"🚨 Ultimato da diretoria: faça {DiretoriaCriterios.UltimatoPontos} pontos nos próximos " +
                            $"{DiretoriaCriterios.UltimatoJogos} jogos ou ela vai pedir a sua demissão.",
                            $"/teams/details/{timeId}", agora);
                        avisados++;
                    }
                }

                // Pedido de demissão vale mesmo para ultimato antigo: é uma decisão que a organização precisa tomar.
                if (u.Situacao != SituacaoUltimato.Fracassado || u.PartidaFinalId is not Guid falha
                    || pedidosGravados.Contains((timeId, falha)))
                    continue;

                var treinador = await _db.TreinadorPassagens.AsNoTracking()
                    .Where(p => p.TimeId == timeId && p.Ate == null && p.Papel == PapelTreinador.Treinador)
                    .OrderByDescending(p => p.Desde)
                    .Select(p => (Guid?)p.TreinadorId)
                    .FirstOrDefaultAsync(ct);

                _db.PedidosDemissao.Add(new PedidoDemissao
                {
                    PedidoId = Guid.NewGuid(),
                    Temporada = m.Painel.Temporada,
                    TimeId = timeId,
                    TreinadorId = treinador,
                    PartidaOrigemId = u.PartidaOrigemId,
                    PartidaFalhaId = falha,
                    Pontos = u.Pontos,
                    Status = StatusPedidoDemissao.Pendente,
                    CriadoEm = agora
                });
                pedidosGravados.Add((timeId, falha));

                AvisosDoTime.Criar(_db, timeId, AvisosDoTime.Diretoria,
                    $"📉 Você não cumpriu o ultimato ({u.Pontos} de {DiretoriaCriterios.UltimatoPontos} pontos). " +
                    "A diretoria pediu a sua demissão; a organização vai conversar com você antes de decidir.",
                    "/minha-area", agora);
                avisados++;
            }
        }

        return avisados;
    }

    // ── Demissões ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PedidoDemissaoDto>> ListPedidosAsync(int temporada, CancellationToken ct)
    {
        var m = await MontarAsync(temporada, ct);
        var confianca = m.Painel.Times.ToDictionary(t => t.TimeId, t => t.Confianca);

        var pedidos = await _db.PedidosDemissao.AsNoTracking()
            .Where(p => p.Temporada == temporada)
            .Select(p => new { p.PedidoId, p.Temporada, p.TimeId, Time = p.Time.TeamName, Treinador = p.Treinador != null ? p.Treinador.Nome : null,
                               p.Pontos, p.CriadoEm, p.Status, p.DecididoEm })
            .ToListAsync(ct);

        return pedidos
            .OrderBy(p => p.Status != StatusPedidoDemissao.Pendente)
            .ThenByDescending(p => p.CriadoEm)
            .Select(p => new PedidoDemissaoDto(p.PedidoId, p.Temporada, p.TimeId, p.Time, p.Treinador, p.Pontos,
                p.CriadoEm, p.Status, p.DecididoEm, confianca.GetValueOrDefault(p.TimeId, DiretoriaCriterios.ConfiancaInicial)))
            .ToArray();
    }

    public async Task DecidirDemissaoAsync(Guid pedidoId, bool aceitar, CancellationToken ct)
    {
        var pedido = await _db.PedidosDemissao.Include(p => p.Treinador).FirstOrDefaultAsync(p => p.PedidoId == pedidoId, ct)
            ?? throw new InvalidOperationException("Pedido de demissão não encontrado.");
        if (pedido.Status != StatusPedidoDemissao.Pendente)
            throw new InvalidOperationException("Esse pedido já foi decidido.");

        var agora = _time.GetUtcNow().UtcDateTime;

        if (aceitar)
        {
            // O treinador sai do clube pelo mesmo caminho de Gerenciar Treinadores (a carreira registra a saída).
            if (_treinadores is null)
                throw new InvalidOperationException("Serviço de treinadores indisponível.");

            var passagens = await _db.TreinadorPassagens.AsNoTracking()
                .Where(p => p.TimeId == pedido.TimeId && p.Ate == null && p.Papel == PapelTreinador.Treinador)
                .Select(p => p.PassagemId)
                .ToListAsync(ct);
            var hoje = HorarioDeBrasilia.Agora(_time);
            foreach (var passagemId in passagens)
                await _treinadores.EncerrarPassagemAsync(passagemId, hoje, ct, MotivoSaidaTreinador.Demitido);
        }
        else
        {
            AvisosDoTime.Criar(_db, pedido.TimeId, AvisosDoTime.Diretoria,
                $"🤝 A organização recusou o pedido de demissão: a diretoria te deu um voto de confiança " +
                $"(confiança {DiretoriaCriterios.ConfiancaVotoDeConfianca:0}). Aproveite!",
                $"/teams/details/{pedido.TimeId}", agora);
        }

        pedido.Status = aceitar ? StatusPedidoDemissao.Aceito : StatusPedidoDemissao.Recusado;
        pedido.DecididoEm = agora;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Até quanto tempo depois do jogo a queda de faixa ainda vira aviso.</summary>
    private static readonly TimeSpan AvisoDeFaixaVale = TimeSpan.FromDays(2);

    // ── Metas ──────────────────────────────────────────────────────────────

    public async Task<DiretoriaPainelDto> GerarMetasAsync(int temporada, CancellationToken ct)
    {
        var ligas = await _db.Ligas.AsNoTracking()
            .Where(l => l.Temporada == temporada && (l.Tipo == TipoCompetition.Liga || l.Tipo == TipoCompetition.Copa))
            .ToListAsync(ct);
        if (ligas.Count == 0)
            throw new InvalidOperationException($"A temporada {temporada} não tem liga nem Copa cadastrada.");

        var ligaIds = ligas.Select(l => l.LigaId).ToList();
        if (await _db.DiretoriaPagamentos.AnyAsync(p => ligaIds.Contains(p.LigaId), ct))
            throw new InvalidOperationException("Essa temporada já pagou bônus da diretoria: estorne antes de gerar as metas de novo.");

        var forcaXI = await ForcaXIAsync(ct);
        var historico = await HistoricoAsync(temporada, ct);
        var agora = _time.GetUtcNow().UtcDateTime;
        var novas = new List<MetaDiretoria>();

        foreach (var liga in ligas.Where(l => l.Tipo == TipoCompetition.Liga && l.Divisao is not null))
        {
            var times = await TimesDaLigaAsync(liga.LigaId, ct);
            var regra = LigaRegraZonas.De(liga.Tipo, liga.Divisao, liga.VagasDiretas, liga.VagasPlayoff);
            var xi = times.ToDictionary(id => id, id => forcaXI.GetValueOrDefault(id));

            foreach (var meta in Diretoria.MetasDaLiga(temporada, liga.Divisao!.Value, regra, xi, historico))
                novas.Add(new MetaDiretoria
                {
                    MetaId = Guid.NewGuid(),
                    Temporada = temporada,
                    LigaId = liga.LigaId,
                    TimeId = meta.TimeId,
                    PosicaoEsperada = meta.PosicaoEsperada,
                    Nota = meta.Nota,
                    MediaHistorico = meta.MediaHistorico,
                    ForcaXI = meta.ForcaXI,
                    MetaPosicao = meta.MetaPosicao,
                    CriadaEm = agora
                });
        }

        foreach (var copa in ligas.Where(l => l.Tipo == TipoCompetition.Copa))
        {
            var potes = await _db.LigaCopaPotes.AsNoTracking()
                .Where(p => p.LigaId == copa.LigaId)
                .Select(p => new { p.TimeId, p.Pote })
                .ToListAsync(ct);

            foreach (var p in potes)
                novas.Add(new MetaDiretoria
                {
                    MetaId = Guid.NewGuid(),
                    Temporada = temporada,
                    LigaId = copa.LigaId,
                    TimeId = p.TimeId,
                    Pote = p.Pote,
                    MetaFase = Diretoria.MetaDaCopa(p.Pote),
                    CriadaEm = agora
                });
        }

        if (novas.Count == 0)
            throw new InvalidOperationException("Nenhum time inscrito nas competições da temporada.");

        var antigas = await _db.MetasDiretoria.Where(m => m.Temporada == temporada).ToListAsync(ct);
        _db.MetasDiretoria.RemoveRange(antigas);
        _db.MetasDiretoria.AddRange(novas);
        await _db.SaveChangesAsync(ct);

        return (await MontarAsync(temporada, ct)).Painel;
    }

    public async Task AjustarMetaAsync(Guid metaId, int? metaPosicao, FasePremiacao? metaFase, CancellationToken ct)
    {
        var meta = await _db.MetasDiretoria.Include(m => m.Liga).FirstOrDefaultAsync(m => m.MetaId == metaId, ct)
            ?? throw new InvalidOperationException("Meta não encontrada.");

        if (await _db.DiretoriaPagamentos.AnyAsync(p => p.LigaId == meta.LigaId, ct))
            throw new InvalidOperationException("O bônus dessa competição já foi pago: estorne antes de mudar a meta.");

        if (meta.Liga.Tipo == TipoCompetition.Liga)
        {
            var total = (await TimesDaLigaAsync(meta.LigaId, ct)).Count;
            if (metaPosicao is not int posicao || posicao < 1 || posicao > total)
                throw new ArgumentException($"A meta da liga precisa ser uma posição de 1 a {total}.");
            meta.MetaPosicao = posicao;
        }
        else
        {
            meta.MetaFase = metaFase;
        }

        meta.AjustadaPeloAdmin = true;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Média de overall dos 11 melhores do elenco atual de cada time.</summary>
    private async Task<Dictionary<Guid, double>> ForcaXIAsync(CancellationToken ct)
    {
        var elencos = await _db.TeamRosters.AsNoTracking()
            .Select(r => new { r.TeamId, r.Player.Overall })
            .ToListAsync(ct);

        return elencos
            .GroupBy(e => e.TeamId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.Overall).OrderByDescending(o => o).Take(DiretoriaCriterios.JogadoresNoXI).Average());
    }

    /// <summary>Posição final de cada time nas ligas encerradas antes da temporada (com mata-mata, vale a final).</summary>
    private async Task<IReadOnlyList<DiretoriaHistoricoInput>> HistoricoAsync(int temporada, CancellationToken ct)
    {
        var tabelas = await _db.LigaClassificacoes.AsNoTracking()
            .Where(c => c.Liga.Tipo == TipoCompetition.Liga
                        && c.Liga.Divisao != null
                        && c.Liga.Temporada != null
                        && c.Liga.Temporada < temporada
                        && c.Liga.Status == LigaStatus.Encerrada
                        && c.Grupo == null)
            .Select(c => new { c.LigaId, Temporada = c.Liga.Temporada!.Value, Divisao = c.Liga.Divisao!.Value, c.TimeId, c.Posicao })
            .ToListAsync(ct);

        var ligaIds = tabelas.Select(c => c.LigaId).Distinct().ToList();
        var mataMata = (await _db.LigaKnockoutJogos.AsNoTracking()
                .Where(k => ligaIds.Contains(k.LigaId))
                .Select(k => new { k.LigaId, Jogo = new JogoKnockoutInput(k.Fase, k.TimeCasaId, k.TimeForaId, k.VencedorId) })
                .ToListAsync(ct))
            .GroupBy(k => k.LigaId)
            .ToDictionary(g => g.Key, g => g.Select(k => k.Jogo).ToList());

        var timesNaSerieA = tabelas
            .Where(c => c.Divisao == Divisao.SerieA)
            .GroupBy(c => c.Temporada)
            .ToDictionary(g => g.Key, g => g.Select(c => c.TimeId).Distinct().Count());

        var historico = new List<DiretoriaHistoricoInput>();
        foreach (var liga in tabelas.GroupBy(c => c.LigaId))
        {
            var tabela = liga.ToDictionary(c => c.TimeId, c => c.Posicao);
            var finais = mataMata.TryGetValue(liga.Key, out var jogos) ? PosicaoFinalLiga.Calcular(tabela, jogos) : tabela;

            foreach (var c in liga)
                historico.Add(new DiretoriaHistoricoInput(
                    c.TimeId, c.Temporada, c.Divisao, finais.GetValueOrDefault(c.TimeId, c.Posicao),
                    timesNaSerieA.GetValueOrDefault(c.Temporada)));
        }

        return historico;
    }

    /// <summary>Times de uma liga: inscritos e, para ligas antigas sem inscrição, os da tabela.</summary>
    private async Task<IReadOnlyList<Guid>> TimesDaLigaAsync(Guid ligaId, CancellationToken ct)
    {
        var inscritos = await _db.LigaTimes.AsNoTracking()
            .Where(t => t.LigaId == ligaId).Select(t => t.TimeId).ToListAsync(ct);
        var daTabela = await _db.LigaClassificacoes.AsNoTracking()
            .Where(c => c.LigaId == ligaId && c.Grupo == null).Select(c => c.TimeId).ToListAsync(ct);

        return inscritos.Concat(daTabela).Distinct().ToArray();
    }

    // ── Painel ─────────────────────────────────────────────────────────────

    private sealed record LigaInfo(
        Guid LigaId, string Nome, TipoCompetition Tipo, Divisao? Divisao, LigaStatus Status,
        int? VagasDiretas, int? VagasPlayoff, Guid? CampeaoTimeId);

    private sealed record Montagem(
        DiretoriaPainelDto Painel,
        Dictionary<Guid, List<PontoConfianca>> Pontos,
        IReadOnlyDictionary<Guid, string> Nomes,
        IReadOnlyDictionary<Guid, DateTime> UltimoJogoPorLiga,
        IReadOnlyDictionary<Guid, IReadOnlyList<UltimatoCalculado>> Ultimatos,
        IReadOnlyList<PedidoDemissao> Pedidos);

    private async Task<Montagem> MontarAsync(int temporada, CancellationToken ct)
    {
        var nomes = await _db.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);

        var ligas = await _db.Ligas.AsNoTracking()
            .Where(l => l.Temporada == temporada)
            .Select(l => new LigaInfo(l.LigaId, l.Nome, l.Tipo, l.Divisao, l.Status, l.VagasDiretas, l.VagasPlayoff, l.CampeaoTimeId))
            .ToListAsync(ct);
        var ligaIds = ligas.Select(l => l.LigaId).ToList();

        var metas = await _db.MetasDiretoria.AsNoTracking()
            .Where(m => m.Temporada == temporada)
            .ToListAsync(ct);

        // Decisões sobre pedidos de demissão mexem na confiança: voto de confiança ou técnico novo.
        var pedidos = await _db.PedidosDemissao.AsNoTracking()
            .Include(p => p.Treinador)
            .Where(p => p.Temporada == temporada)
            .ToListAsync(ct);
        var ajustes = pedidos
            .Where(p => p.Status is StatusPedidoDemissao.Aceito or StatusPedidoDemissao.Recusado && p.DecididoEm is not null)
            .Select(p => new DiretoriaAjusteInput(p.TimeId, p.DecididoEm!.Value,
                p.Status == StatusPedidoDemissao.Aceito ? EventoDiretoria.NovoTecnico : EventoDiretoria.VotoDeConfianca))
            .ToList();
        ajustes.AddRange(await SaidasDeTreinadorAsync(temporada, ligas, ajustes, ct));

        var (pontos, ultimoJogo) = await ConfiancaAsync(temporada, ajustes, ct);
        var ultimatos = pontos.ToDictionary(p => p.Key, p => Diretoria.Ultimatos(p.Key, p.Value));

        // Quem joga a temporada: inscritos nas ligas, nos grupos da Copa e quem já tem meta.
        var divisaoPorTime = (await _db.LigaTimes.AsNoTracking()
                .Where(t => ligaIds.Contains(t.LigaId) && t.Liga.Tipo == TipoCompetition.Liga)
                .Select(t => new { t.TimeId, t.Liga.Divisao })
                .ToListAsync(ct))
            .GroupBy(t => t.TimeId)
            .ToDictionary(g => g.Key, g => g.Min(t => t.Divisao));
        var daCopa = await _db.LigaGruposTimes.AsNoTracking()
            .Where(g => ligaIds.Contains(g.LigaId)).Select(g => g.TimeId).ToListAsync(ct);

        var timeIds = divisaoPorTime.Keys
            .Concat(daCopa)
            .Concat(metas.Select(m => m.TimeId))
            .Concat(pontos.Keys)
            .Distinct()
            .ToList();

        var situacoes = await SituacoesAsync(ligas, metas, ct);

        var times = timeIds
            .Select(id =>
            {
                var historico = pontos.GetValueOrDefault(id) ?? new List<PontoConfianca>();
                var confianca = historico.Count > 0 ? historico[^1].Valor : DiretoriaCriterios.ConfiancaInicial;

                var metasDoTime = metas
                    .Where(m => m.TimeId == id)
                    .Select(m => situacoes[m.MetaId])
                    .OrderBy(m => m.Tipo)
                    .ToArray();

                return new DiretoriaTimeDto(
                    id, nomes.GetValueOrDefault(id, "?"), divisaoPorTime.GetValueOrDefault(id),
                    confianca, Diretoria.Faixa(confianca),
                    historico.Count > 0 ? historico[^1].Variacao : null,
                    metasDoTime,
                    historico.Select(p => new DiretoriaPontoDto(
                        p.PartidaId, p.Data, p.Competicao, p.AdversarioId, nomes.GetValueOrDefault(p.AdversarioId, "?"),
                        p.GolsPro, p.GolsContra, p.Variacao, p.Valor, p.Evento)).ToArray(),
                    ultimatos.GetValueOrDefault(id)?.LastOrDefault(u => u.Situacao == SituacaoUltimato.EmAndamento) is { } u
                        ? new DiretoriaUltimatoDto(u.Inicio, u.Pontos, u.Jogos)
                        : null);
            })
            .OrderByDescending(t => t.Confianca)
            .ThenBy(t => t.TimeNome, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return new Montagem(new DiretoriaPainelDto(temporada, metas.Count > 0, times), pontos, nomes, ultimoJogo, ultimatos, pedidos);
    }

    /// <summary>
    /// Treinador que saiu do clube durante a temporada (pediu demissão, foi demitido ou outro): quem chega começa
    /// do zero, como num pedido de demissão aceito. Saída depois de a temporada acabar não mexe nela.
    /// </summary>
    private async Task<List<DiretoriaAjusteInput>> SaidasDeTreinadorAsync(
        int temporada, IReadOnlyList<LigaInfo> ligas, IReadOnlyList<DiretoriaAjusteInput> jaTem, CancellationToken ct)
    {
        if (ligas.Count == 0) return new List<DiretoriaAjusteInput>();
        var ligaIds = ligas.Select(l => l.LigaId).ToList();
        var inicio = await _db.Ligas.Where(l => ligaIds.Contains(l.LigaId)).MinAsync(l => l.CriadoEm, ct);
        DateTime? fim = null;
        if (ligas.All(l => l.Status == LigaStatus.Encerrada))
            fim = await _db.LigaPartidas
                .Where(p => ligaIds.Contains(p.Rodada.LigaId) && p.Status == PartidaStatus.Encerrada)
                .MaxAsync(p => (DateTime?)(p.EncerradaEm ?? p.Rodada.DataHora), ct);

        var saidas = await _db.TreinadorPassagens.AsNoTracking()
            .Where(p => p.Papel == PapelTreinador.Treinador && p.Ate != null)
            .Select(p => new { p.TimeId, p.Ate, p.SaiuEm })
            .ToListAsync(ct);

        return saidas
            // Sem o momento exato (passagens antigas): fim do dia da saída no horário de Brasília.
            .Select(s => new { s.TimeId, Quando = s.SaiuEm ?? s.Ate!.Value.Date.AddDays(1).AddHours(3) })
            .Where(s => s.Quando >= inicio && (fim is null || s.Quando <= fim))
            // Pedido aceito já trouxe o técnico novo nesse momento.
            .Where(s => !jaTem.Any(a => a.TimeId == s.TimeId && a.Evento == EventoDiretoria.NovoTecnico
                                        && Math.Abs((a.Data - s.Quando).TotalHours) < 1))
            .Select(s => new DiretoriaAjusteInput(s.TimeId, s.Quando, EventoDiretoria.NovoTecnico))
            .ToList();
    }

    /// <summary>
    /// Confiança de cada time em cada jogo da temporada. A chance esperada sai do Elo de todos os jogos
    /// até ali (as temporadas anteriores também contam), com o mesmo cálculo do Power Ranking.
    /// </summary>
    private async Task<(Dictionary<Guid, List<PontoConfianca>> Pontos, Dictionary<Guid, DateTime> UltimoJogoPorLiga)> ConfiancaAsync(
        int temporada, IReadOnlyList<DiretoriaAjusteInput> ajustes, CancellationToken ct)
    {
        // W.O. não diz nada sobre a força do time e fica de fora, como no Power Ranking.
        var partidas = await _db.LigaPartidas.AsNoTracking()
            .Where(p => p.Status == PartidaStatus.Encerrada && !p.IsWO)
            .Select(p => new
            {
                p.PartidaId, p.TimeCasaId, p.TimeForaId, p.GolsCasa, p.GolsFora, p.EncerradaEm,
                p.Rodada.DataHora, p.Rodada.Numero, p.Rodada.LigaId,
                p.Rodada.Liga.Temporada, LigaCriadaEm = p.Rodada.Liga.CriadoEm,
                p.Rodada.Liga.Tipo, p.Rodada.Liga.Divisao
            })
            .ToListAsync(ct);

        var emOrdem = partidas
            .Select(p => new { P = p, Quando = p.EncerradaEm ?? p.DataHora ?? p.LigaCriadaEm.AddMinutes(p.Numero) })
            .OrderBy(x => x.Quando)
            .ToList();

        var esperados = PowerRanking.EsperadoCasa(emOrdem
            .Select(x => new PowerRankingPartidaInput(
                x.P.TimeCasaId, x.P.TimeForaId, x.P.GolsCasa, x.P.GolsFora,
                x.P.Temporada ?? x.P.LigaCriadaEm.Year, DateOnly.FromDateTime(x.Quando.AddHours(-3)), x.Quando))
            .ToList());

        var daTemporada = emOrdem
            .Select((x, i) => (x.P, x.Quando, Esperado: esperados[i]))
            .Where(x => (x.P.Temporada ?? x.P.LigaCriadaEm.Year) == temporada)
            .ToList();

        var jogos = daTemporada.Select(x => new DiretoriaJogoInput(
            x.P.PartidaId,
            x.P.Numero == NumeroPlayoffAcesso ? "Playoff de acesso" : LigaLabels.Competicao(x.P.Tipo, x.P.Divisao),
            x.Quando, x.P.TimeCasaId, x.P.TimeForaId, x.P.GolsCasa, x.P.GolsFora, x.Esperado));

        var ultimoJogo = daTemporada
            .GroupBy(x => x.P.LigaId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Quando));

        return (Diretoria.Confianca(jogos, ajustes), ultimoJogo);
    }

    /// <summary>Situação de cada meta: onde o time está e se cumpriu, superou ou não cumpriu.</summary>
    private async Task<Dictionary<Guid, DiretoriaMetaDto>> SituacoesAsync(
        IReadOnlyList<LigaInfo> ligas, IReadOnlyList<MetaDiretoria> metas, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, DiretoriaMetaDto>();
        if (metas.Count == 0) return resultado;

        var porLiga = ligas.ToDictionary(l => l.LigaId);
        var ligaIds = metas.Select(m => m.LigaId).Distinct().ToList();

        var tabelas = (await _db.LigaClassificacoes.AsNoTracking()
                .Where(c => ligaIds.Contains(c.LigaId) && c.Grupo == null)
                .Select(c => new { c.LigaId, c.TimeId, c.Posicao, c.Jogos })
                .ToListAsync(ct))
            .GroupBy(c => c.LigaId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(c => c.TimeId, c => (c.Posicao, c.Jogos)));

        var subiuNoPlayoff = await PlayoffDeAcessoAsync(ligas, ct);
        var copas = await CopasAsync(ligas.Where(l => l.Tipo == TipoCompetition.Copa && ligaIds.Contains(l.LigaId)).ToList(), ct);

        foreach (var meta in metas)
        {
            if (!porLiga.TryGetValue(meta.LigaId, out var liga)) continue;

            if (liga.Tipo == TipoCompetition.Liga && liga.Divisao is Divisao divisao && meta.MetaPosicao is int metaPosicao)
            {
                var tabela = tabelas.GetValueOrDefault(meta.LigaId);
                var total = tabela?.Count ?? 0;
                var regra = LigaRegraZonas.De(liga.Tipo, liga.Divisao, liga.VagasDiretas, liga.VagasPlayoff);
                var limite = Diretoria.LimiteSerieA(regra, total);
                var encerrada = liga.Status == LigaStatus.Encerrada;

                SituacaoMeta situacao = SituacaoMeta.EmAndamento;
                string? andamento = "Ainda não começou";
                bool? dentro = null;

                if (tabela is not null && tabela.TryGetValue(meta.TimeId, out var linha))
                {
                    situacao = Diretoria.AvaliarLiga(divisao, regra, metaPosicao, linha.Posicao, encerrada,
                        subiuNoPlayoff.TryGetValue(meta.TimeId, out var subiu) ? subiu : null);

                    if (encerrada)
                        andamento = situacao == SituacaoMeta.EmAndamento
                            ? $"Terminou em {linha.Posicao}º · falta o playoff"
                            : $"Terminou em {linha.Posicao}º";
                    else if (linha.Jogos > 0)
                    {
                        andamento = $"Hoje: {linha.Posicao}º";
                        dentro = linha.Posicao <= metaPosicao;
                    }
                }

                resultado[meta.MetaId] = new DiretoriaMetaDto(
                    meta.MetaId, liga.LigaId, liga.Nome, liga.Tipo, liga.Divisao,
                    DiretoriaLabels.MetaLiga(divisao, metaPosicao, limite),
                    metaPosicao, null, null, meta.PosicaoEsperada, meta.Nota, meta.MediaHistorico, meta.ForcaXI,
                    situacao, andamento, dentro, meta.AjustadaPeloAdmin);
            }
            else if (liga.Tipo == TipoCompetition.Copa)
            {
                var copa = copas.GetValueOrDefault(liga.LigaId);
                var fase = copa?.Fases.GetValueOrDefault(meta.TimeId) ?? FasePremiacao.FaseDeGrupos;
                var vivo = copa is null || copa.Vivos.Contains(meta.TimeId);

                resultado[meta.MetaId] = new DiretoriaMetaDto(
                    meta.MetaId, liga.LigaId, liga.Nome, liga.Tipo, null,
                    DiretoriaLabels.MetaCopa(meta.MetaFase),
                    null, meta.MetaFase, meta.Pote, null, null, null, null,
                    Diretoria.AvaliarCopa(meta.MetaFase, fase, vivo),
                    copa is null || !copa.Comecou ? "Ainda não começou" : DiretoriaLabels.FaseCopa(fase, vivo),
                    null, meta.AjustadaPeloAdmin);
            }
        }

        return resultado;
    }

    /// <summary>Times da Série B que jogaram o playoff de acesso: true = subiu; ausente = não jogou ou ainda não acabou.</summary>
    private async Task<Dictionary<Guid, bool>> PlayoffDeAcessoAsync(IReadOnlyList<LigaInfo> ligas, CancellationToken ct)
    {
        var serieA = ligas.Where(l => l.Tipo == TipoCompetition.Liga && l.Divisao == Divisao.SerieA).Select(l => l.LigaId).ToList();
        var serieB = ligas.Where(l => l.Tipo == TipoCompetition.Liga && l.Divisao == Divisao.SerieB).Select(l => l.LigaId).ToList();
        var daB = (await _db.LigaTimes.AsNoTracking()
                .Where(t => serieB.Contains(t.LigaId)).Select(t => t.TimeId).ToListAsync(ct))
            .ToHashSet();

        var jogos = await _db.LigaPartidas.AsNoTracking()
            .Where(p => serieA.Contains(p.Rodada.LigaId) && p.Rodada.Numero == NumeroPlayoffAcesso && p.Status == PartidaStatus.Encerrada)
            .Select(p => new { p.TimeCasaId, p.TimeForaId, p.GolsCasa, p.GolsFora, p.TemPenaltis, p.PenaltisVencedorId })
            .ToListAsync(ct);

        var resultado = new Dictionary<Guid, bool>();
        foreach (var j in jogos)
        {
            var vencedor = LigaDesempate.VencedorDoJogoDecisivo(j.TimeCasaId, j.TimeForaId, j.GolsCasa, j.GolsFora, j.TemPenaltis, j.PenaltisVencedorId);
            if (vencedor is null) continue;

            foreach (var timeId in new[] { j.TimeCasaId, j.TimeForaId }.Where(daB.Contains))
                resultado[timeId] = vencedor == timeId;
        }

        return resultado;
    }

    private sealed record CopaInfo(Dictionary<Guid, FasePremiacao> Fases, HashSet<Guid> Vivos, bool Comecou);

    /// <summary>
    /// Até onde cada time chegou em cada Copa e quem ainda está vivo: cai quem perde no mata-mata e,
    /// com a fase de grupos encerrada, quem ficou fora do chaveamento.
    /// </summary>
    private async Task<Dictionary<Guid, CopaInfo>> CopasAsync(IReadOnlyList<LigaInfo> copas, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, CopaInfo>();

        foreach (var copa in copas)
        {
            var jogos = await _db.LigaKnockoutJogos.AsNoTracking()
                .Where(k => k.LigaId == copa.LigaId)
                .Select(k => new { k.PartidaId, Jogo = new JogoKnockoutInput(k.Fase, k.TimeCasaId, k.TimeForaId, k.VencedorId) })
                .ToListAsync(ct);

            var participantes = await _db.LigaGruposTimes.AsNoTracking()
                .Where(g => g.LigaId == copa.LigaId).Select(g => g.TimeId).ToListAsync(ct);
            if (participantes.Count == 0)
                participantes = await _db.LigaCopaPotes.AsNoTracking()
                    .Where(p => p.LigaId == copa.LigaId).Select(p => p.TimeId).ToListAsync(ct);

            var partidas = await _db.LigaPartidas.AsNoTracking()
                .Where(p => p.Rodada.LigaId == copa.LigaId)
                .Select(p => new { p.PartidaId, Encerrada = p.Status == PartidaStatus.Encerrada })
                .ToListAsync(ct);

            var doMataMata = jogos.Where(j => j.PartidaId != null).Select(j => j.PartidaId!.Value).ToHashSet();
            var gruposEncerrados = partidas.Count > 0 && partidas.Where(p => !doMataMata.Contains(p.PartidaId)).All(p => p.Encerrada);
            var noChaveamento = jogos
                .SelectMany(j => new[] { j.Jogo.TimeCasaId, j.Jogo.TimeForaId })
                .Where(id => id is not null).Select(id => id!.Value).ToHashSet();
            var eliminados = jogos
                .Where(j => j.Jogo.VencedorId is not null)
                .SelectMany(j => new[] { j.Jogo.TimeCasaId, j.Jogo.TimeForaId }.Where(id => id is not null && id != j.Jogo.VencedorId))
                .Select(id => id!.Value)
                .ToHashSet();

            var encerrada = copa.Status == LigaStatus.Encerrada;
            var vivos = participantes
                .Where(id => !encerrada
                             && !eliminados.Contains(id)
                             && !(gruposEncerrados && noChaveamento.Count > 0 && !noChaveamento.Contains(id)))
                .ToHashSet();

            // Sem chaveamento ainda, todo mundo está na fase de grupos (FasesDoMataMata trataria como
            // jogo único de Supercopa, em que o outro time é o vice).
            var fases = jogos.Count == 0
                ? participantes.Distinct().ToDictionary(id => id, _ => FasePremiacao.FaseDeGrupos)
                : FasesDoMataMata.Calcular(jogos.Select(j => j.Jogo), copa.CampeaoTimeId, participantes);

            resultado[copa.LigaId] = new CopaInfo(
                fases,
                vivos,
                partidas.Any(p => p.Encerrada));
        }

        return resultado;
    }

    /// <summary>
    /// Ultimato dado e cumprido viram notícia; o fracasso não (o pedido de demissão é sigiloso até a
    /// organização decidir). A decisão vira notícia quando é tomada.
    /// </summary>
    private static IEnumerable<PlantaoNoticiaDto> NoticiasDeUltimatos(Montagem m)
    {
        foreach (var (timeId, ultimatos) in m.Ultimatos)
        {
            var time = m.Nomes.GetValueOrDefault(timeId, "?");
            var link = $"/teams/details/{timeId}";

            foreach (var u in ultimatos)
            {
                yield return new PlantaoNoticiaDto(
                    u.Inicio.AddSeconds(5), PlantaoCategoria.Diretoria, "🚨", $"Ultimato no {time}!",
                    $"A diretoria exige {DiretoriaCriterios.UltimatoPontos} pontos nos próximos {DiretoriaCriterios.UltimatoJogos} jogos",
                    link, time);

                if (u.Situacao == SituacaoUltimato.Cumprido && u.Fim is DateTime fim)
                    yield return new PlantaoNoticiaDto(
                        fim.AddSeconds(5), PlantaoCategoria.Diretoria, "😮‍💨", $"{time} cumpre o ultimato e o técnico respira",
                        $"{u.Pontos} pontos em {u.Jogos} {(u.Jogos == 1 ? "jogo" : "jogos")}", link, time);
            }
        }

        foreach (var p in m.Pedidos.Where(p => p.DecididoEm is not null))
        {
            var time = m.Nomes.GetValueOrDefault(p.TimeId, "?");
            var tecnico = p.Treinador?.Nome;
            yield return p.Status == StatusPedidoDemissao.Aceito
                ? new PlantaoNoticiaDto(p.DecididoEm!.Value, PlantaoCategoria.Diretoria, "🚪",
                    tecnico is null ? $"{time} demite o técnico" : $"{time} demite {tecnico}",
                    "Ultimato não cumprido: a diretoria levou a melhor", $"/teams/details/{p.TimeId}", time)
                : new PlantaoNoticiaDto(p.DecididoEm!.Value, PlantaoCategoria.Diretoria, "🤝",
                    tecnico is null ? $"Diretoria do {time} mantém o técnico" : $"Diretoria do {time} mantém {tecnico}",
                    $"Voto de confiança depois do ultimato: confiança volta a {DiretoriaCriterios.ConfiancaVotoDeConfianca:0}",
                    $"/teams/details/{p.TimeId}", time);
        }
    }

    /// <summary>Fim da liga: quem superou ou não cumpriu a meta vira notícia (cumprir é o esperado).</summary>
    private static IEnumerable<PlantaoNoticiaDto> NoticiasDeMetas(Montagem m)
    {
        foreach (var time in m.Painel.Times)
            foreach (var meta in time.Metas.Where(x => x.Tipo == TipoCompetition.Liga))
            {
                if (!m.UltimoJogoPorLiga.TryGetValue(meta.LigaId, out var data)) continue;

                var (emoji, manchete) = meta.Situacao switch
                {
                    SituacaoMeta.Superada => ("🌟", $"{time.TimeNome} supera a meta da diretoria"),
                    SituacaoMeta.Fracassada => ("📉", $"{time.TimeNome} fica abaixo da meta da diretoria"),
                    _ => ((string?)null, (string?)null)
                };
                if (manchete is null) continue;

                yield return new PlantaoNoticiaDto(
                    data.AddSeconds(4), PlantaoCategoria.Diretoria, emoji!, manchete,
                    $"{LigaLabels.Competicao(meta.Tipo, meta.Divisao)}: a meta era {meta.Meta.ToLowerInvariant()} · {meta.Andamento?.ToLowerInvariant()}",
                    $"/teams/details/{time.TimeId}", time.TimeNome);
            }
    }

    // ── Bônus ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<DiretoriaBonusPreviaDto>> ListBonusAsync(int temporada, CancellationToken ct)
    {
        var montagem = await MontarAsync(temporada, ct);
        var ligaIds = montagem.Painel.Times.SelectMany(t => t.Metas).Select(m => m.LigaId).Distinct().ToList();

        var ligas = await _db.Ligas.AsNoTracking()
            .Where(l => ligaIds.Contains(l.LigaId))
            .OrderBy(l => l.Tipo).ThenBy(l => l.Divisao)
            .ToListAsync(ct);

        var previas = new List<DiretoriaBonusPreviaDto>(ligas.Count);
        foreach (var liga in ligas)
            previas.Add(await PreviaAsync(liga, montagem.Painel, ct));

        return previas;
    }

    public async Task<DiretoriaBonusPreviaDto> PagarBonusAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(l => l.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Competição não encontrada.");
        if (liga.Temporada is not int temporada)
            throw new InvalidOperationException("A competição não tem temporada definida.");

        var previa = await PreviaAsync(liga, (await MontarAsync(temporada, ct)).Painel, ct);

        if (previa.JaPago)
            throw new InvalidOperationException("O bônus dessa competição já foi pago.");
        if (!previa.Encerrada)
            throw new InvalidOperationException("A competição precisa estar encerrada para pagar o bônus.");
        if (previa.Impedimento is not null)
            throw new InvalidOperationException(previa.Impedimento);
        if (previa.Linhas.Count == 0)
            throw new InvalidOperationException("Ninguém cumpriu a meta nessa competição.");

        var agora = _time.GetUtcNow().UtcDateTime;
        var timeIds = previa.Linhas.Select(l => l.TimeId).ToList();
        var times = await _db.Teams.Where(t => timeIds.Contains(t.TeamId)).ToListAsync(ct);

        foreach (var linha in previa.Linhas)
        {
            var time = times.FirstOrDefault(t => t.TeamId == linha.TimeId)
                ?? throw new InvalidOperationException($"Time {linha.TimeNome} não encontrado.");

            time.Budget = decimal.Round(time.Budget + linha.Valor, 2, MidpointRounding.AwayFromZero);

            _db.DiretoriaPagamentos.Add(new DiretoriaPagamento
            {
                PagamentoId = Guid.NewGuid(),
                LigaId = ligaId,
                TimeId = linha.TimeId,
                Valor = linha.Valor,
                Motivo = linha.Motivo,
                PagoEm = agora
            });

            _db.BudgetLedgers.Add(new BudgetLedger
            {
                BudgetLedgerId = Guid.NewGuid(),
                TeamId = linha.TimeId,
                DataUtc = agora,
                Tipo = "CREDIT",
                Origem = OrigemLedger,
                Valor = linha.Valor,
                Descricao = $"Bônus da diretoria — {liga.Nome} — {linha.Motivo}"
            });
        }

        await _db.SaveChangesAsync(ct);

        return await PreviaAsync(liga, (await MontarAsync(temporada, ct)).Painel, ct);
    }

    public async Task EstornarBonusAsync(Guid ligaId, CancellationToken ct)
    {
        var pagamentos = await _db.DiretoriaPagamentos.Where(p => p.LigaId == ligaId).ToListAsync(ct);
        if (pagamentos.Count == 0)
            throw new InvalidOperationException("Essa competição não tem bônus da diretoria pago.");

        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(l => l.LigaId == ligaId, ct);
        var timeIds = pagamentos.Select(p => p.TimeId).ToList();
        var times = await _db.Teams.Where(t => timeIds.Contains(t.TeamId)).ToListAsync(ct);
        var agora = _time.GetUtcNow().UtcDateTime;

        foreach (var pagamento in pagamentos)
        {
            var time = times.FirstOrDefault(t => t.TeamId == pagamento.TimeId);
            if (time is null) continue;

            time.Budget = decimal.Round(Math.Max(time.Budget - pagamento.Valor, 0m), 2, MidpointRounding.AwayFromZero);

            _db.BudgetLedgers.Add(new BudgetLedger
            {
                BudgetLedgerId = Guid.NewGuid(),
                TeamId = pagamento.TimeId,
                DataUtc = agora,
                Tipo = "DEBIT",
                Origem = OrigemLedger,
                Valor = pagamento.Valor,
                Descricao = $"Estorno do bônus da diretoria — {liga?.Nome ?? "competição"} — {pagamento.Motivo}"
            });
        }

        _db.DiretoriaPagamentos.RemoveRange(pagamentos);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<DiretoriaBonusPreviaDto> PreviaAsync(Liga liga, DiretoriaPainelDto painel, CancellationToken ct)
    {
        var encerrada = liga.Status == LigaStatus.Encerrada;

        var pagamentos = await _db.DiretoriaPagamentos.AsNoTracking()
            .Where(p => p.LigaId == liga.LigaId)
            .Include(p => p.Time)
            .ToListAsync(ct);

        // Já pago: a prévia mostra exatamente o que foi creditado.
        if (pagamentos.Count > 0)
            return new DiretoriaBonusPreviaDto(
                liga.LigaId, liga.Nome, liga.Tipo, liga.Divisao, encerrada, true, pagamentos.Max(p => p.PagoEm), null,
                pagamentos.OrderByDescending(p => p.Valor)
                    .Select(p => new PremiacaoLinhaDto(p.TimeId, p.Time.TeamName, p.Motivo, p.Valor))
                    .ToArray());

        DiretoriaBonusPreviaDto Impedida(string motivo) =>
            new(liga.LigaId, liga.Nome, liga.Tipo, liga.Divisao, encerrada, false, null, motivo, Array.Empty<PremiacaoLinhaDto>());

        var premiacao = await _db.Premiacoes.AsNoTracking().FirstOrDefaultAsync(p => p.Temporada == liga.Temporada, ct);
        var (cumprida, superada) = liga.Tipo == TipoCompetition.Liga
            ? (premiacao?.BonusMetaLigaCumprida ?? 0, premiacao?.BonusMetaLigaSuperada ?? 0)
            : (premiacao?.BonusMetaCopaCumprida ?? 0, premiacao?.BonusMetaCopaSuperada ?? 0);

        if (cumprida <= 0 && superada <= 0)
            return Impedida($"A temporada {liga.Temporada} não tem bônus da diretoria para {LigaLabels.Competicao(liga.Tipo, liga.Divisao)} (cadastre em Gerenciar Premiação).");

        var metas = painel.Times
            .SelectMany(t => t.Metas.Where(m => m.LigaId == liga.LigaId).Select(m => (Time: t, Meta: m)))
            .ToList();

        if (encerrada && metas.Any(x => x.Meta.Situacao == SituacaoMeta.EmAndamento))
            return Impedida("Ainda falta o playoff de acesso para fechar as metas.");

        var linhas = metas
            .Select(x => x.Meta.Situacao switch
            {
                SituacaoMeta.Superada when superada > 0 =>
                    new PremiacaoLinhaDto(x.Time.TimeId, x.Time.TimeNome, $"Meta superada — {x.Meta.Meta}", superada),
                SituacaoMeta.Cumprida when cumprida > 0 =>
                    new PremiacaoLinhaDto(x.Time.TimeId, x.Time.TimeNome, $"Meta cumprida — {x.Meta.Meta}", cumprida),
                _ => null
            })
            .Where(l => l is not null)
            .Select(l => l!)
            .OrderByDescending(l => l.Valor).ThenBy(l => l.TimeNome)
            .ToArray();

        return new DiretoriaBonusPreviaDto(
            liga.LigaId, liga.Nome, liga.Tipo, liga.Divisao, encerrada, false, null, null,
            encerrada ? linhas : Array.Empty<PremiacaoLinhaDto>());
    }
}
