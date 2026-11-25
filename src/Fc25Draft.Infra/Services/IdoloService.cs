using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>Ídolos dos clubes (regras em <see cref="Idolos"/>).</summary>
public class IdoloService : IIdoloService
{
    private readonly DraftDbContext _db;
    private readonly ILigaPublicService _ligas;
    private readonly TimeProvider _time;

    /// <summary>Separa os motivos na coluna (o texto de um motivo já usa " · ").</summary>
    private const string Separador = " | ";

    public IdoloService(DraftDbContext db, ILigaPublicService ligas, TimeProvider? time = null)
    {
        _db = db;
        _ligas = ligas;
        _time = time ?? TimeProvider.System;
    }

    public async Task<int> AtualizarAsync(CancellationToken ct)
    {
        var resultados = Idolos.Calcular(await EstatisticasAsync(ct), await TitulosAsync(ct));
        var agora = _time.GetUtcNow().UtcDateTime;

        var existentes = await _db.Idolos.ToListAsync(ct);
        // Na primeira vez a regra só reconhece quem já é ídolo: sem aviso de "virou ídolo" para todo mundo.
        var primeiraVez = existentes.Count == 0;
        var porChave = existentes.ToDictionary(i => (i.JogadorId, i.TimeId));
        var onde = await OndeEstaoAsync(ct);

        var ids = resultados.Select(r => r.JogadorId).Concat(existentes.Select(e => e.JogadorId)).Distinct().ToList();
        var nomes = await _db.Players.AsNoTracking().Where(p => ids.Contains(p.PlayerId))
            .ToDictionaryAsync(p => p.PlayerId, p => p.Name, ct);
        var times = await _db.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);

        var mudancas = 0;
        foreach (var r in resultados)
        {
            var motivos = string.Join(Separador, r.Motivos);
            if (motivos.Length > 500) motivos = motivos[..497] + "...";

            if (porChave.TryGetValue((r.JogadorId, r.TimeId), out var idolo))
            {
                if (idolo.Jogos == r.Jogos && idolo.Gols == r.Gols && idolo.Assistencias == r.Assistencias
                    && idolo.Titulos == r.Titulos && idolo.Motivos == motivos)
                    continue;
                (idolo.Jogos, idolo.Gols, idolo.Assistencias, idolo.Titulos, idolo.Motivos) =
                    (r.Jogos, r.Gols, r.Assistencias, r.Titulos, motivos);
                mudancas++;
                continue;
            }

            idolo = new Idolo
            {
                IdoloId = Guid.NewGuid(), JogadorId = r.JogadorId, TimeId = r.TimeId,
                Jogos = r.Jogos, Gols = r.Gols, Assistencias = r.Assistencias, Titulos = r.Titulos,
                Motivos = motivos, ReconhecidoEm = agora
            };
            _db.Idolos.Add(idolo);
            porChave[(r.JogadorId, r.TimeId)] = idolo;
            mudancas++;

            // Já tinha saído do clube: entra para a história do clube, sem custo para ninguém.
            if (!onde.NoClube(r.JogadorId, r.TimeId))
            {
                var (quando, saida) = await SaidaAsync(r.JogadorId, r.TimeId, onde, ct);
                idolo.SaiuEm = quando ?? agora;
                idolo.Saida = saida;
            }
            else if (!primeiraVez)
            {
                AvisosDoTime.Criar(_db, r.TimeId, AvisosDoTime.Idolo,
                    $"👑 {nomes.GetValueOrDefault(r.JogadorId, "?")} virou ídolo do {times.GetValueOrDefault(r.TimeId, "clube")}: {string.Join(", ", r.Motivos)}.",
                    $"/players/details/{r.JogadorId}", agora);
            }
        }

        // Ídolo que deixou o clube depois de reconhecido.
        foreach (var idolo in porChave.Values.Where(i => i.SaiuEm is null && !onde.NoClube(i.JogadorId, i.TimeId)).ToList())
        {
            var (quando, saida) = await SaidaAsync(idolo.JogadorId, idolo.TimeId, onde, ct);
            idolo.SaiuEm = quando ?? agora;
            idolo.Saida = saida;
            idolo.PerdeuConfianca = saida is SaidaDoIdolo.Vendido or SaidaDoIdolo.Troca && idolo.SaiuEm >= idolo.ReconhecidoEm;
            mudancas++;

            if (idolo.PerdeuConfianca)
                AvisosDoTime.Criar(_db, idolo.TimeId, AvisosDoTime.Idolo,
                    $"😠 A diretoria não gostou de ver o ídolo {nomes.GetValueOrDefault(idolo.JogadorId, "?")} "
                    + $"{(saida == SaidaDoIdolo.Troca ? "trocado" : "vendido")}: "
                    + $"{IdolosCriterios.SaidaNaConfianca:0} de confiança.",
                    $"/teams/details/{idolo.TimeId}", agora);
        }

        if (mudancas > 0) await _db.SaveChangesAsync(ct);
        return mudancas;
    }

    public async Task<IReadOnlyList<IdoloDto>> ListarAsync(CancellationToken ct) => await MontarAsync(null, null, ct);

    public async Task<IReadOnlyList<IdoloDto>> DoJogadorAsync(int jogadorId, CancellationToken ct) =>
        await MontarAsync(jogadorId, null, ct);

    public async Task<IReadOnlySet<int>> DoTimeAsync(Guid timeId, CancellationToken ct) =>
        (await _db.Idolos.AsNoTracking().Where(i => i.TimeId == timeId).Select(i => i.JogadorId).ToListAsync(ct)).ToHashSet();

    private async Task<IReadOnlyList<IdoloDto>> MontarAsync(int? jogadorId, Guid? timeId, CancellationToken ct)
    {
        var linhas = await _db.Idolos.AsNoTracking()
            .Where(i => (jogadorId == null || i.JogadorId == jogadorId) && (timeId == null || i.TimeId == timeId))
            .Select(i => new
            {
                i.IdoloId, i.JogadorId, i.Jogador.Name, Posicao = i.Jogador.Position.Name, i.Jogador.Overall,
                Aposentado = i.Jogador.AposentadoEm != null,
                i.TimeId, i.Time.TeamName, i.Jogos, i.Gols, i.Assistencias, i.Titulos, i.Motivos, i.ReconhecidoEm,
                i.SaiuEm, i.Saida, i.PerdeuConfianca,
                TimeAtual = i.Jogador.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault()
            })
            .ToListAsync(ct);
        if (linhas.Count == 0) return Array.Empty<IdoloDto>();

        var onde = await OndeEstaoAsync(ct);
        return linhas
            .Select(l => new IdoloDto(
                l.IdoloId, l.JogadorId, l.Name, l.Posicao, l.Overall, l.TimeId, l.TeamName,
                l.Jogos, l.Gols, l.Assistencias, l.Titulos,
                l.Motivos.Split(Separador, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                l.ReconhecidoEm, onde.NoClube(l.JogadorId, l.TimeId), l.Aposentado, l.TimeAtual,
                l.SaiuEm, l.Saida, l.PerdeuConfianca))
            .OrderBy(i => i.TimeNome, StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(i => i.NoClube)
            .ThenByDescending(i => i.Jogos + i.Gols + i.Assistencias + i.Titulos * 10)
            .ToList();
    }

    // ── Onde cada jogador está ─────────────────────────────────────────────

    private sealed record Onde(Dictionary<int, Guid> Elenco, Dictionary<int, Guid> EmprestadoPor, HashSet<int> Aposentados)
    {
        /// <summary>No elenco do clube ou emprestado por ele.</summary>
        public bool NoClube(int jogador, Guid time) =>
            Elenco.GetValueOrDefault(jogador) == time || EmprestadoPor.GetValueOrDefault(jogador) == time;
    }

    private async Task<Onde> OndeEstaoAsync(CancellationToken ct)
    {
        var elenco = (await _db.TeamRosters.AsNoTracking().Select(r => new { r.PlayerId, r.TeamId }).ToListAsync(ct))
            .GroupBy(r => r.PlayerId).ToDictionary(g => g.Key, g => g.First().TeamId);
        var emprestados = (await _db.Emprestimos.AsNoTracking()
                .Where(e => e.Status == EmprestimoStatus.Ativo)
                .Select(e => new { e.PlayerId, e.DonoTeamId })
                .ToListAsync(ct))
            .GroupBy(e => e.PlayerId).ToDictionary(g => g.Key, g => g.First().DonoTeamId);
        var aposentados = (await _db.Players.AsNoTracking().Where(p => p.AposentadoEm != null).Select(p => p.PlayerId)
            .ToListAsync(ct)).ToHashSet();
        return new Onde(elenco, emprestados, aposentados);
    }

    /// <summary>Quando e como ele deixou o clube: a última saída registrada (empréstimo não conta).</summary>
    private async Task<(DateTime? Quando, SaidaDoIdolo Saida)> SaidaAsync(int jogadorId, Guid timeId, Onde onde, CancellationToken ct)
    {
        var t = await _db.TransferHistories.AsNoTracking()
            .Where(h => h.PlayerId == jogadorId && h.FromTeamId == timeId && h.Type != TransferType.Loan)
            .OrderByDescending(h => h.PerformedAtUtc)
            .Select(h => new { h.PerformedAtUtc, h.Type })
            .FirstOrDefaultAsync(ct);

        var saida = t?.Type switch
        {
            TransferType.TeamSale or TransferType.QuickSell or TransferType.LoanPurchase or TransferType.MarketAuction => SaidaDoIdolo.Vendido,
            TransferType.TeamTrade => SaidaDoIdolo.Troca,
            TransferType.Aposentadoria => SaidaDoIdolo.Aposentou,
            TransferType.ExpansionDraft => SaidaDoIdolo.DraftDeExpansao,
            _ => onde.Aposentados.Contains(jogadorId) ? SaidaDoIdolo.Aposentou : SaidaDoIdolo.Outra
        };
        // A coluna é timestamptz: dependendo da configuração do Npgsql ela volta no fuso da máquina. Aqui tudo é UTC.
        DateTime? quando = t?.PerformedAtUtc switch
        {
            null => null,
            { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
            { } outro => DateTime.SpecifyKind(outro, DateTimeKind.Utc)
        };
        return (quando, saida);
    }

    // ── Números ────────────────────────────────────────────────────────────

    /// <summary>
    /// Jogos, gols, assistências e nota média de cada jogador por clube e competição. Jogos: titular no encerramento ou
    /// entrou por substituição (o mesmo contador da carreira), mais os anotados à mão nas competições sem contador.
    /// </summary>
    private async Task<List<Idolos.Estatistica>> EstatisticasAsync(CancellationToken ct)
    {
        var titulares = await _db.LigaEscalacoes.AsNoTracking()
            .Where(e => e.Titular)
            .Select(e => new { e.JogadorId, e.TimeId, e.PartidaId, e.Partida.Rodada.LigaId })
            .ToListAsync(ct);
        var entraram = await _db.LigaEventos.AsNoTracking()
            .Where(e => e.Tipo == TipoEvento.Substituicao)
            .Select(e => new { e.JogadorId, e.TimeId, e.PartidaId, e.Partida.Rodada.LigaId })
            .ToListAsync(ct);
        var ligasComContagem = titulares.Select(t => t.LigaId).ToHashSet();

        var jogos = titulares.Concat(entraram).Distinct()
            .GroupBy(p => (p.JogadorId, p.TimeId, p.LigaId))
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (var a in await _db.JogosAnotados.AsNoTracking().ToListAsync(ct))
            if (!ligasComContagem.Contains(a.LigaId))
                jogos[(a.JogadorId, a.TimeId, a.LigaId)] = jogos.GetValueOrDefault((a.JogadorId, a.TimeId, a.LigaId)) + a.Jogos;

        var gols = await _db.LigaEventos.AsNoTracking()
            .Where(e => e.Tipo == TipoEvento.Gol)
            .Select(e => new { e.JogadorId, e.AssistenteId, e.TimeId, e.Partida.Rodada.LigaId })
            .ToListAsync(ct);
        var golsPor = gols.GroupBy(g => (g.JogadorId, g.TimeId, g.LigaId)).ToDictionary(g => g.Key, g => g.Count());
        var assistPor = gols.Where(g => g.AssistenteId != null)
            .GroupBy(g => (g.AssistenteId!.Value, g.TimeId, g.LigaId)).ToDictionary(g => g.Key, g => g.Count());

        var notas = (await _db.LigaNotasJogadores.AsNoTracking()
                .GroupBy(n => new { n.JogadorId, n.TimeId, n.Partida.Rodada.LigaId })
                .Select(g => new { g.Key.JogadorId, g.Key.TimeId, g.Key.LigaId, Media = g.Average(n => n.Nota), Jogos = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(n => (n.JogadorId, n.TimeId, n.LigaId));

        return jogos.Keys.Concat(golsPor.Keys).Concat(assistPor.Keys).Concat(notas.Keys).Distinct()
            .Select(k => new Idolos.Estatistica(k.Item1, k.Item2, k.Item3,
                jogos.GetValueOrDefault(k), golsPor.GetValueOrDefault(k), assistPor.GetValueOrDefault(k),
                notas.TryGetValue(k, out var n) ? n.Media : null, n?.Jogos ?? 0))
            .ToList();
    }

    /// <summary>Títulos que fazem ídolo: Série A, Série B e Copa (a Supercopa é um jogo só).</summary>
    private async Task<List<Idolos.Titulo>> TitulosAsync(CancellationToken ct)
    {
        var edicoes = (await _ligas.ListEdicoesAsync(ct))
            .Where(e => e.CampeaoTimeId != null && e.Tipo is TipoCompetition.Liga or TipoCompetition.Copa)
            .ToList();
        if (edicoes.Count == 0) return new List<Idolos.Titulo>();

        var ligaIds = edicoes.Select(e => e.LigaId).ToList();
        var jogosDoTime = (await _db.LigaPartidas.AsNoTracking()
                .Where(p => p.Status == PartidaStatus.Encerrada && ligaIds.Contains(p.Rodada.LigaId))
                .Select(p => new { p.Rodada.LigaId, p.TimeCasaId, p.TimeForaId })
                .ToListAsync(ct))
            .SelectMany(p => new[] { (p.LigaId, p.TimeCasaId), (p.LigaId, p.TimeForaId) })
            .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        return edicoes
            .Select(e => new Idolos.Titulo(e.LigaId, e.Nome, e.CampeaoTimeId!.Value,
                jogosDoTime.GetValueOrDefault((e.LigaId, e.CampeaoTimeId!.Value))))
            .ToList();
    }
}
