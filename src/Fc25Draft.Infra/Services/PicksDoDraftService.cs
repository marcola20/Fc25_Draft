using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Picks do próximo draft (1ª e 2ª rodada de cada time da temporada). Negociáveis nas propostas até a loteria desse
/// draft acontecer; quando o draft é gerado, cada pick vai para o dono.
/// </summary>
public class PicksDoDraftService : IPicksDoDraftService
{
    public static readonly int[] Rodadas = [1, 2];

    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public PicksDoDraftService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<PicksDoProximoDraftDto> ProximoDraftAsync(CancellationToken ct)
    {
        var agora = HorarioDeBrasilia.Agora(_time);
        var atual = await TemporadaAtualAsync(_db, agora, ct);
        var picks = new List<PickDoDraftDto>();
        foreach (var t in await TemporadasNegociaveisAsync(_db, agora, ct))
            picks.AddRange(await PicksAsync(_db, t, ct));
        return new PicksDoProximoDraftDto(atual, picks.Any(p => !p.Usada), picks);
    }

    public Task<int> ResolverProtecoesAsync(CancellationToken ct) => ResolverProtecoesAsync(_db, _time.GetUtcNow().UtcDateTime, ct);

    public async Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct)
    {
        var negocios = await _db.TransferOffers.AsNoTracking()
            .Where(o => o.Status == OfferStatus.Accepted && o.Picks.Any())
            .Select(o => new
            {
                o.OfferId, o.UpdatedAtUtc, De = o.FromTeam.TeamName, Para = o.ToTeam.TeamName,
                Picks = o.Picks.Select(p => new { p.Rodada, Original = p.TimeOriginal.TeamName, p.IsTarget }).ToList(),
                Jogadores = o.Players.Count
            })
            .ToListAsync(ct);

        return negocios.Select(n =>
        {
            // Quem recebe cada pick: as pedidas (IsTarget) vão para quem propôs.
            var paraQuemPropos = n.Picks.Where(p => p.IsTarget).Select(p => $"{p.Rodada}ª rodada do {p.Original}").ToList();
            var paraOOutro = n.Picks.Where(p => !p.IsTarget).Select(p => $"{p.Rodada}ª rodada do {p.Original}").ToList();
            var titulo = paraQuemPropos.Count > 0
                ? $"{n.De} fica com a pick da {string.Join(" e da ", paraQuemPropos)} no negócio com o {n.Para}"
                : $"{n.Para} fica com a pick da {string.Join(" e da ", paraOOutro)} no negócio com o {n.De}";
            var linha = paraQuemPropos.Count > 0 && paraOOutro.Count > 0
                ? $"Na volta, o {n.Para} leva a da {string.Join(" e da ", paraOOutro)}"
                : n.Jogadores > 0 ? "Negócio com jogadores envolvidos" : "Negócio só de picks do próximo draft";
            return new PlantaoNoticiaDto(n.UpdatedAtUtc, PlantaoCategoria.Mercado, "🔄", titulo, linha, "/picks", n.De, null);
        }).ToList();
    }

    // ── Para as propostas e o draft ────────────────────────────────────────────

    /// <summary>Temporada em andamento (a Série A mais recente que já começou): é dela o próximo draft.</summary>
    public static async Task<int?> TemporadaAtualAsync(DraftDbContext db, DateTime agoraBrasilia, CancellationToken ct) =>
        await db.Ligas.AsNoTracking()
            .Where(l => l.Tipo == TipoCompetition.Liga && l.DataInicio <= agoraBrasilia
                        && (l.Divisao == null || l.Divisao == Divisao.SerieA))
            .OrderByDescending(l => l.DataInicio)
            .Select(l => l.Temporada)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Drafts cujas picks se negociam: o da temporada em andamento (até a última pick dele ser usada) e o de uma
    /// temporada anterior que ainda está acontecendo (a virada pode vir no meio do draft).
    /// </summary>
    public static async Task<List<int>> TemporadasNegociaveisAsync(DraftDbContext db, DateTime agoraBrasilia, CancellationToken ct)
    {
        var temporadas = new List<int>();
        if (await TemporadaAtualAsync(db, agoraBrasilia, ct) is int atual
            && !await db.Drafts.AnyAsync(d => d.Temporada == atual && d.Picks.All(p => p.PlayerId != null), ct))
            temporadas.Add(atual);
        temporadas.AddRange(await db.Drafts.AsNoTracking()
            .Where(d => d.Temporada != null && !temporadas.Contains(d.Temporada.Value) && d.Picks.Any(p => p.PlayerId == null))
            .Select(d => d.Temporada!.Value)
            .ToListAsync(ct));
        return temporadas;
    }

    /// <summary>Posição de cada time em cada rodada, se a loteria do draft da temporada já terminou: (rodada, time) → posição.</summary>
    public static async Task<Dictionary<(int Rodada, Guid Time), int>> PosicoesAsync(DraftDbContext db, int temporada, CancellationToken ct)
    {
        var serieA = await db.Ligas.AsNoTracking()
            .Where(l => l.Temporada == temporada && l.Tipo == TipoCompetition.Liga && (l.Divisao == null || l.Divisao == Divisao.SerieA))
            .Select(l => l.LigaId)
            .FirstOrDefaultAsync(ct);
        var picks = await db.LigaLoterias.AsNoTracking()
            .Where(l => l.LigaId == serieA && l.IsFinished)
            .OrderByDescending(l => l.CriadoEm)
            .Take(1)
            .SelectMany(l => l.Picks)
            .Select(p => new { p.Rodada, p.TimeId, p.PickNumero })
            .ToListAsync(ct);
        return picks.GroupBy(p => (p.Rodada, p.TimeId)).ToDictionary(g => g.Key, g => g.First().PickNumero);
    }

    /// <summary>Todas as picks do draft da temporada, com o dono, a posição (depois da loteria) e a proteção.</summary>
    public static async Task<IReadOnlyList<PickDoDraftDto>> PicksAsync(DraftDbContext db, int temporada, CancellationToken ct)
    {
        var times = await db.LigaTimes.AsNoTracking()
            .Where(t => t.Liga.Temporada == temporada && t.Liga.Tipo == TipoCompetition.Liga)
            .Select(t => new { t.TimeId, t.Time.TeamName })
            .Distinct()
            .ToListAsync(ct);
        var nomes = await db.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);
        var registros = (await db.PicksDoDraft.AsNoTracking().Where(p => p.Temporada == temporada).ToListAsync(ct))
            .ToDictionary(p => (p.Rodada, p.TimeOriginalId));
        var posicoes = await PosicoesAsync(db, temporada, ct);
        var usadas = (await db.DraftPicks.AsNoTracking()
                .Where(p => p.Draft.Temporada == temporada && p.PlayerId != null)
                .Select(p => new { p.RoundNumber, p.PickInRound })
                .ToListAsync(ct))
            .Select(p => (p.RoundNumber, p.PickInRound))
            .ToHashSet();

        return times
            .SelectMany(t => Rodadas.Select(r =>
            {
                var registro = registros.GetValueOrDefault((r, t.TimeId));
                var dono = registro?.DonoId ?? t.TimeId;
                int? posicao = posicoes.TryGetValue((r, t.TimeId), out var pos) ? pos : null;
                return new PickDoDraftDto(temporada, r, t.TimeId, t.TeamName, dono, nomes.GetValueOrDefault(dono, "?"),
                    posicao, posicao is int x && usadas.Contains((r, x)),
                    registro?.ProtegidaAte, registro?.ProtetorId is Guid protetor ? nomes.GetValueOrDefault(protetor) : null,
                    registro?.Compensacao, registro?.ProtecaoAcionada);
            }))
            .OrderBy(p => p.Temporada).ThenBy(p => p.Rodada).ThenBy(p => p.Posicao ?? int.MaxValue).ThenBy(p => p.TimeOriginalNome)
            .ToList();
    }

    /// <summary>Dono de cada pick negociada do draft da temporada: (rodada, time original) → dono.</summary>
    public static async Task<Dictionary<(int Rodada, Guid TimeOriginal), Guid>> DonosAsync(DraftDbContext db, int temporada, CancellationToken ct) =>
        (await db.PicksDoDraft.AsNoTracking()
            .Where(p => p.Temporada == temporada)
            .Select(p => new { p.Rodada, p.TimeOriginalId, p.DonoId })
            .ToListAsync(ct))
        .ToDictionary(p => (p.Rodada, p.TimeOriginalId), p => p.DonoId);

    /// <summary>
    /// Confere que as picks ainda se negociam (não foram usadas) e são todas de <paramref name="dono"/>, e que a
    /// proteção pedida faz sentido (antes da loteria, numa pick que ainda não tem). Devolve o problema ou nulo.
    /// </summary>
    public static async Task<string?> ProblemaAsync(DraftDbContext db, DateTime agoraBrasilia, IEnumerable<PickRefDto> picks,
        Guid dono, string nomeDoDono, CancellationToken ct, IReadOnlyDictionary<PickRefDto, PickProtecaoDto>? protecoes = null)
    {
        var lista = picks.ToList();
        if (lista.Count == 0) return null;

        var negociaveis = await TemporadasNegociaveisAsync(db, agoraBrasilia, ct);
        var todas = new Dictionary<PickRefDto, PickDoDraftDto>();
        foreach (var t in lista.Select(p => p.Temporada).Distinct().Where(negociaveis.Contains))
            foreach (var pick in await PicksAsync(db, t, ct))
                todas[pick.Ref] = pick;

        foreach (var p in lista)
        {
            if (!todas.TryGetValue(p, out var pick)) return "Essa pick não se negocia mais (o draft dela já acabou).";
            if (pick.Usada) return $"A pick {pick.Rotulo} já foi usada no draft.";
            if (pick.DonoId != dono) return $"A pick da {pick.Rotulo} não é do {nomeDoDono}.";
            if (protecoes?.GetValueOrDefault(p) is { } protecao)
            {
                if (pick.Posicao is not null) return $"A pick {pick.Rotulo} já tem posição: proteção só antes da loteria.";
                if (pick.ProtegidaAte is not null && pick.ProtecaoAcionada is null)
                    return $"A pick da {pick.Rotulo} já tem proteção (até a #{pick.ProtegidaAte}).";
                if (protecao.ProtegidaAte < 1) return "A proteção tem que ser até uma posição (#1 ou mais).";
                if (protecao.Compensacao < 0) return "A devolução da proteção não pode ser negativa.";
            }
        }
        return null;
    }

    /// <summary>
    /// Passa as picks de <paramref name="antigoDono"/> para <paramref name="novoDono"/>, com a proteção que vier
    /// junto (quem cede protege). Se o draft já foi gerado, quem escolhe naquela posição muda também. Não salva.
    /// </summary>
    public static async Task TransferirAsync(DraftDbContext db, IEnumerable<TransferOfferPick> picks, Guid antigoDono, Guid novoDono,
        DateTime agora, CancellationToken ct)
    {
        foreach (var p in picks)
        {
            var registro = await db.PicksDoDraft.FirstOrDefaultAsync(
                x => x.Temporada == p.Temporada && x.Rodada == p.Rodada && x.TimeOriginalId == p.TimeOriginalId, ct);
            if (registro is null)
            {
                registro = new PickDoDraft
                {
                    PickDoDraftId = Guid.NewGuid(), Temporada = p.Temporada, Rodada = p.Rodada, TimeOriginalId = p.TimeOriginalId
                };
                db.PicksDoDraft.Add(registro);
            }
            // Pela Entry: com os times já carregados no contexto, mudar só o campo deixaria o EF voltar o vínculo antigo.
            db.Entry(registro).Property(x => x.DonoId).CurrentValue = novoDono;
            registro.NegociadaEm = agora;
            if (p.ProtegidaAte is int ate)
            {
                registro.ProtegidaAte = ate;
                registro.ProtetorId = antigoDono;
                registro.Compensacao = p.Compensacao ?? 0m;
                registro.ProtecaoResolvidaEm = null;
                registro.ProtecaoAcionada = null;
            }

            await MudarNoDraftAsync(db, p.Temporada, p.Rodada, p.TimeOriginalId, novoDono, ct);
        }
    }

    /// <summary>Se o draft da temporada já foi gerado e a pick não foi usada, quem escolhe nela passa a ser o dono.</summary>
    private static async Task MudarNoDraftAsync(DraftDbContext db, int temporada, int rodada, Guid timeOriginal, Guid dono, CancellationToken ct)
    {
        var posicoes = await PosicoesAsync(db, temporada, ct);
        if (!posicoes.TryGetValue((rodada, timeOriginal), out var posicao)) return;
        var noDraft = await db.DraftPicks.FirstOrDefaultAsync(
            x => x.Draft.Temporada == temporada && x.RoundNumber == rodada && x.PickInRound == posicao && x.PlayerId == null, ct);
        if (noDraft is not null) db.Entry(noDraft).Property(x => x.TeamId).CurrentValue = dono;
    }

    /// <summary>
    /// Depois da loteria, confere as picks protegidas: a que caiu dentro da proteção volta para quem protegeu, que
    /// paga a devolução a quem estava com ela. Salva. Retorna quantas voltaram.
    /// </summary>
    public static async Task<int> ResolverProtecoesAsync(DraftDbContext db, DateTime agoraUtc, CancellationToken ct)
    {
        var pendentes = await db.PicksDoDraft
            .Where(p => p.ProtegidaAte != null && p.ProtecaoResolvidaEm == null)
            .ToListAsync(ct);
        if (pendentes.Count == 0) return 0;

        var voltaram = 0;
        var nomes = await db.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);
        foreach (var temporada in pendentes.GroupBy(p => p.Temporada))
        {
            var posicoes = await PosicoesAsync(db, temporada.Key, ct);
            if (posicoes.Count == 0) continue; // a loteria ainda não terminou

            foreach (var pick in temporada)
            {
                if (!posicoes.TryGetValue((pick.Rodada, pick.TimeOriginalId), out var posicao)) continue;
                pick.ProtecaoResolvidaEm = agoraUtc;
                pick.ProtecaoAcionada = posicao <= pick.ProtegidaAte;
                var rotulo = $"#{posicao} da {pick.Rodada}ª rodada (do {nomes.GetValueOrDefault(pick.TimeOriginalId)})";
                if (pick.ProtecaoAcionada != true || pick.ProtetorId is not Guid protetorId || protetorId == pick.DonoId)
                    continue;

                // Caiu dentro da proteção: a pick volta e quem protegeu devolve o combinado.
                var quemPerde = pick.DonoId;
                db.Entry(pick).Property(x => x.DonoId).CurrentValue = protetorId;
                await MudarNoDraftAsync(db, pick.Temporada, pick.Rodada, pick.TimeOriginalId, protetorId, ct);
                if (pick.Compensacao is > 0 && await db.Teams.FirstOrDefaultAsync(t => t.TeamId == protetorId, ct) is { } protetor
                    && await db.Teams.FirstOrDefaultAsync(t => t.TeamId == quemPerde, ct) is { } perdeu)
                {
                    var valor = pick.Compensacao.Value;
                    protetor.Budget = decimal.Round(protetor.Budget - valor, 2, MidpointRounding.AwayFromZero);
                    perdeu.Budget = decimal.Round(perdeu.Budget + valor, 2, MidpointRounding.AwayFromZero);
                    var descricao = $"Proteção da pick {rotulo}: a pick voltou para o {protetor.TeamName}";
                    ExtratoCaixa.Lancar(db, protetorId, -valor, ExtratoCaixa.Transferencia, descricao, agoraUtc);
                    ExtratoCaixa.Lancar(db, quemPerde, valor, ExtratoCaixa.Transferencia, descricao, agoraUtc);
                }

                var devolucao = pick.Compensacao is > 0 ? $" e devolveu {pick.Compensacao.Value / 1_000_000m:0.#} mi" : "";
                AvisosDoTime.Criar(db, quemPerde, AvisosDoTime.Proposta,
                    $"🛡️ A pick {rotulo} era protegida até a #{pick.ProtegidaAte}: voltou para o {nomes.GetValueOrDefault(protetorId)}{devolucao}.",
                    "/picks", agoraUtc);
                AvisosDoTime.Criar(db, protetorId, AvisosDoTime.Proposta,
                    $"🛡️ A proteção acionou: a pick {rotulo} voltou para você{(devolucao.Length > 0 ? $" (você devolveu {pick.Compensacao!.Value / 1_000_000m:0.#} mi)" : "")}.",
                    "/picks", agoraUtc);
                voltaram++;
            }
        }

        await db.SaveChangesAsync(ct);
        return voltaram;
    }
}
