using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Virada de temporada: playoff de acesso (Série A x Série B) e criação das ligas seguintes
/// com promovidos e rebaixados já no lugar.
/// </summary>
public class LigaTemporadaService : ILigaTemporadaService
{
    /// <summary>Rodada sentinela do playoff de acesso: fica na Série A, fora da classificação.</summary>
    private const int NumeroPlayoffAcesso = -2;

    private readonly DraftDbContext _db;
    private readonly ILigaAdminService _ligas;

    public LigaTemporadaService(DraftDbContext db, ILigaAdminService ligas)
    {
        _db = db;
        _ligas = ligas;
    }

    public async Task<IReadOnlyList<int>> ListTemporadasAsync(CancellationToken ct) =>
        await _db.Ligas.AsNoTracking()
            .Where(l => l.Temporada != null && l.Tipo == TipoCompetition.Liga)
            .Select(l => l.Temporada!.Value)
            .Distinct()
            .OrderByDescending(t => t)
            .ToListAsync(ct);

    public async Task<TemporadaResumoDto?> GetResumoAsync(int temporada, CancellationToken ct)
    {
        var estado = await CarregarAsync(temporada, ct);
        if (estado is null) return null;

        var playoffs = estado.Playoffs.Select(p => p.Dto).ToList();
        var proximaExiste = await _db.Ligas.AnyAsync(l => l.Temporada == temporada + 1 && l.Tipo == TipoCompetition.Liga, ct);

        var impedimento = estado.Impedimento(proximaExiste);
        var podeCriarPlayoff = estado.VagasPlayoff > 0 && estado.Playoffs.Count == 0 && estado.LigasEncerradas && estado.SerieB is not null;
        var timesSemDivisao = await ListarTimesSemDivisaoAsync(temporada, ct);

        return new TemporadaResumoDto(
            temporada,
            estado.SerieA is null ? null : ToDto(estado.SerieA),
            estado.SerieB is null ? null : ToDto(estado.SerieB),
            estado.Times,
            playoffs,
            timesSemDivisao,
            podeCriarPlayoff,
            impedimento is null,
            impedimento,
            proximaExiste);
    }

    public async Task<IReadOnlyList<TemporadaPlayoffDto>> CriarPlayoffAcessoAsync(int temporada, CancellationToken ct)
    {
        var estado = await CarregarAsync(temporada, ct)
            ?? throw new InvalidOperationException($"Nenhuma liga encontrada na temporada {temporada}.");

        if (estado.SerieA is null || estado.SerieB is null)
            throw new InvalidOperationException("O playoff de acesso precisa de Série A e Série B na mesma temporada.");
        if (!estado.LigasEncerradas)
            throw new InvalidOperationException("Encerre as duas divisões antes de criar o playoff de acesso.");
        if (estado.VagasPlayoff == 0)
            throw new InvalidOperationException("Nenhuma das divisões tem vaga de playoff configurada.");
        if (estado.Playoffs.Count > 0)
            throw new InvalidOperationException("O playoff de acesso já foi criado.");

        var rodada = new LigaRodada
        {
            RodadaId = Guid.NewGuid(),
            LigaId = estado.SerieA.LigaId,
            Numero = NumeroPlayoffAcesso,
            Desempate = true
        };
        _db.LigaRodadas.Add(rodada);

        foreach (var (timeA, timeB) in estado.DuplasDePlayoff())
        {
            rodada.Partidas.Add(new LigaPartida
            {
                PartidaId = Guid.NewGuid(),
                RodadaId = rodada.RodadaId,
                TimeCasaId = timeA,
                TimeForaId = timeB,
                Status = PartidaStatus.Agendada
            });
        }

        await _db.SaveChangesAsync(ct);

        var atualizado = await CarregarAsync(temporada, ct);
        return atualizado!.Playoffs.Select(p => p.Dto).ToList();
    }

    public async Task<IReadOnlyList<LigaDto>> GerarProximaTemporadaAsync(GerarProximaTemporadaRequest request, CancellationToken ct)
    {
        var estado = await CarregarAsync(request.TemporadaOrigem, ct)
            ?? throw new InvalidOperationException($"Nenhuma liga encontrada na temporada {request.TemporadaOrigem}.");

        var proxima = request.TemporadaOrigem + 1;
        var jaExiste = await _db.Ligas.AnyAsync(l => l.Temporada == proxima && l.Tipo == TipoCompetition.Liga, ct);
        var impedimento = estado.Impedimento(jaExiste);
        if (impedimento is not null)
            throw new InvalidOperationException(impedimento);

        if (string.IsNullOrWhiteSpace(request.NomeSerieA))
            throw new InvalidOperationException("Informe o nome da Série A da próxima temporada.");
        if (request.CriarSerieB && string.IsNullOrWhiteSpace(request.NomeSerieB))
            throw new InvalidOperationException("Informe o nome da Série B da próxima temporada.");

        var extras = (request.TimesExtrasSerieB ?? Array.Empty<Guid>()).Distinct().ToList();
        var jaNaTemporada = estado.Times.Select(t => t.TimeId).ToHashSet();
        if (extras.Any(id => jaNaTemporada.Contains(id)))
            throw new InvalidOperationException("Um time que já disputou a temporada não pode entrar como time extra.");

        var timesSerieA = estado.Times.Where(t => t.DivisaoProxima == Divisao.SerieA).Select(t => t.TimeId).ToList();
        var timesSerieB = estado.Times.Where(t => t.DivisaoProxima == Divisao.SerieB).Select(t => t.TimeId).Concat(extras).ToList();

        if (timesSerieA.Count < 2)
            throw new InvalidOperationException("A Série A da próxima temporada ficaria com menos de 2 times.");
        if (request.CriarSerieB && timesSerieB.Count < 2)
            throw new InvalidOperationException("A Série B da próxima temporada ficaria com menos de 2 times. Escolha os times novos.");
        if (!request.CriarSerieB && timesSerieB.Count > 0)
            throw new InvalidOperationException("Há times destinados à Série B; crie a Série B ou ajuste as vagas da temporada atual.");

        var criadas = new List<LigaDto>();

        var serieA = await _ligas.CreateAsync(new LigaCreateRequest(
            request.NomeSerieA, request.DataInicio, request.DataFim, TipoCompetition.Liga,
            proxima, Divisao.SerieA, request.VagasDiretasSerieA, request.VagasPlayoffSerieA), ct);
        await _ligas.ConfigurarTimesLigaAsync(serieA.LigaId, timesSerieA, ct);
        criadas.Add(serieA);

        if (request.CriarSerieB)
        {
            var serieB = await _ligas.CreateAsync(new LigaCreateRequest(
                request.NomeSerieB!, request.DataInicio, request.DataFim, TipoCompetition.Liga,
                proxima, Divisao.SerieB, request.VagasDiretasSerieB, request.VagasPlayoffSerieB), ct);
            await _ligas.ConfigurarTimesLigaAsync(serieB.LigaId, timesSerieB, ct);
            criadas.Add(serieB);
        }

        return criadas;
    }

    // ── Estado da temporada ──────────────────────────────────────────────────

    private sealed record PlayoffJogo(TemporadaPlayoffDto Dto, Guid TimeSerieAId, Guid TimeSerieBId)
    {
        public bool Decidido => Dto.VencedorId is not null;
        public bool SerieAVenceu => Dto.VencedorId == TimeSerieAId;
    }

    private sealed class EstadoTemporada
    {
        public required int Temporada { get; init; }
        public Liga? SerieA { get; init; }
        public Liga? SerieB { get; init; }
        public required List<TemporadaTimeDto> Times { get; init; }
        public required List<PlayoffJogo> Playoffs { get; init; }
        public required List<(Guid A, Guid B)> Duplas { get; init; }
        public int VagasPlayoff => Duplas.Count;

        public bool LigasEncerradas =>
            SerieA?.Status == LigaStatus.Encerrada && (SerieB is null || SerieB.Status == LigaStatus.Encerrada);

        public IReadOnlyList<(Guid A, Guid B)> DuplasDePlayoff() => Duplas;

        /// <summary>Motivo para não dar para gerar a próxima temporada, ou nulo quando está tudo pronto.</summary>
        public string? Impedimento(bool proximaJaExiste)
        {
            if (SerieA is null) return $"A temporada {Temporada} não tem Série A cadastrada.";
            if (!LigasEncerradas) return "Encerre as competições da temporada antes de gerar a próxima.";
            if (proximaJaExiste) return $"A temporada {Temporada + 1} já tem ligas cadastradas.";
            if (Duplas.Count > 0 && Playoffs.Count == 0) return "Crie o playoff de acesso antes de gerar a próxima temporada.";
            if (Playoffs.Any(p => !p.Decidido)) return "O playoff de acesso ainda não terminou (registre o vencedor, inclusive nos pênaltis).";
            return null;
        }
    }

    private async Task<EstadoTemporada?> CarregarAsync(int temporada, CancellationToken ct)
    {
        var ligas = await _db.Ligas.AsNoTracking()
            .Where(l => l.Temporada == temporada && l.Tipo == TipoCompetition.Liga && l.Divisao != null)
            .ToListAsync(ct);

        var serieA = ligas.FirstOrDefault(l => l.Divisao == Divisao.SerieA);
        var serieB = ligas.FirstOrDefault(l => l.Divisao == Divisao.SerieB);
        if (serieA is null && serieB is null) return null;

        var classifA = await ClassificacaoAsync(serieA, ct);
        var classifB = await ClassificacaoAsync(serieB, ct);

        // Vagas de playoff: o que as duas divisões oferecem (a menor manda).
        var vagasPlayoff = serieB is null
            ? 0
            : Math.Min(
                Math.Min(serieA?.VagasPlayoff ?? 0, serieB.VagasPlayoff ?? 0),
                Math.Min(Math.Max(classifA.Count - (serieA?.VagasDiretas ?? 0) - 1, 0), Math.Max(classifB.Count - (serieB.VagasDiretas ?? 0), 0)));

        var duplas = new List<(Guid A, Guid B)>();
        for (int i = 0; i < vagasPlayoff; i++)
        {
            // Série A: logo acima da zona de queda (o melhor da faixa primeiro). Série B: logo abaixo do acesso direto.
            var idxA = classifA.Count - (serieA?.VagasDiretas ?? 0) - vagasPlayoff + i;
            var idxB = (serieB!.VagasDiretas ?? 0) + i;
            duplas.Add((classifA[idxA].TimeId, classifB[idxB].TimeId));
        }

        var playoffs = serieA is null ? new List<PlayoffJogo>() : await CarregarPlayoffsAsync(serieA.LigaId, ct);
        var times = MontarTimes(serieA, serieB, classifA, classifB, duplas, playoffs);

        return new EstadoTemporada
        {
            Temporada = temporada,
            SerieA = serieA,
            SerieB = serieB,
            Times = times,
            Playoffs = playoffs,
            Duplas = duplas
        };
    }

    private async Task<List<LigaClassificacao>> ClassificacaoAsync(Liga? liga, CancellationToken ct) =>
        liga is null
            ? new List<LigaClassificacao>()
            : await _db.LigaClassificacoes.AsNoTracking().Include(c => c.Time)
                .Where(c => c.LigaId == liga.LigaId)
                .OrderBy(c => c.Posicao)
                .ToListAsync(ct);

    private async Task<List<PlayoffJogo>> CarregarPlayoffsAsync(Guid ligaSerieAId, CancellationToken ct)
    {
        var partidas = await _db.LigaPartidas.AsNoTracking()
            .Include(p => p.TimeCasa).Include(p => p.TimeFora)
            .Where(p => p.Rodada.LigaId == ligaSerieAId && p.Rodada.Numero == NumeroPlayoffAcesso)
            .ToListAsync(ct);

        return partidas.Select(p =>
        {
            var vencedorId = p.Status != PartidaStatus.Encerrada
                ? null
                : LigaDesempate.VencedorDoJogoDecisivo(p.TimeCasaId, p.TimeForaId, p.GolsCasa, p.GolsFora, p.TemPenaltis, p.PenaltisVencedorId);

            var dto = new TemporadaPlayoffDto(
                p.PartidaId,
                p.TimeCasaId, p.TimeCasa.TeamName, p.Status == PartidaStatus.Agendada ? null : p.GolsCasa,
                p.TimeForaId, p.TimeFora.TeamName, p.Status == PartidaStatus.Agendada ? null : p.GolsFora,
                p.Status,
                vencedorId,
                vencedorId is null ? null : vencedorId == p.TimeCasaId ? p.TimeCasa.TeamName : p.TimeFora.TeamName);

            return new PlayoffJogo(dto, p.TimeCasaId, p.TimeForaId);
        }).ToList();
    }

    private static List<TemporadaTimeDto> MontarTimes(
        Liga? serieA, Liga? serieB,
        List<LigaClassificacao> classifA, List<LigaClassificacao> classifB,
        List<(Guid A, Guid B)> duplas, List<PlayoffJogo> playoffs)
    {
        var times = new List<TemporadaTimeDto>();
        times.AddRange(Montar(serieA, classifA, Divisao.SerieA));
        times.AddRange(Montar(serieB, classifB, Divisao.SerieB));
        return times;

        IEnumerable<TemporadaTimeDto> Montar(Liga? liga, List<LigaClassificacao> classif, Divisao divisao)
        {
            if (liga is null) yield break;

            var regra = LigaRegraZonas.De(liga.Tipo, liga.Divisao, liga.VagasDiretas, liga.VagasPlayoff);

            for (int i = 0; i < classif.Count; i++)
            {
                var c = classif[i];
                var zona = LigaZonas.Zona(regra, i + 1, classif.Count);
                var noPlayoff = duplas.Any(d => d.A == c.TimeId || d.B == c.TimeId);
                var jogo = playoffs.FirstOrDefault(p => p.TimeSerieAId == c.TimeId || p.TimeSerieBId == c.TimeId);

                var (proxima, situacao) = (divisao, zona) switch
                {
                    (Divisao.SerieA, ZonaClassificacao.Rebaixamento) => ((Divisao?)Divisao.SerieB, "Rebaixado para a Série B"),
                    (Divisao.SerieB, ZonaClassificacao.CampeaoComAcesso) => (Divisao.SerieA, "Campeão e acesso à Série A"),
                    (Divisao.SerieB, ZonaClassificacao.AcessoDireto) => (Divisao.SerieA, "Acesso à Série A"),
                    _ when noPlayoff => SituacaoPlayoff(divisao, jogo, c.TimeId),
                    _ => (divisao, divisao == Divisao.SerieA ? "Permanece na Série A" : "Permanece na Série B")
                };

                yield return new TemporadaTimeDto(c.TimeId, c.Time.TeamName, divisao, i + 1, zona, proxima, situacao);
            }
        }

        static (Divisao?, string) SituacaoPlayoff(Divisao divisao, PlayoffJogo? jogo, Guid timeId)
        {
            if (jogo is null || !jogo.Decidido)
                return (null, "Playoff de acesso — a disputar");

            var venceu = jogo.Dto.VencedorId == timeId;
            return divisao == Divisao.SerieA
                ? venceu ? (Divisao.SerieA, "Playoff vencido — fica na Série A") : (Divisao.SerieB, "Playoff perdido — cai para a Série B")
                : venceu ? (Divisao.SerieA, "Playoff vencido — sobe para a Série A") : (Divisao.SerieB, "Playoff perdido — fica na Série B");
        }
    }

    private async Task<List<TemporadaTimeLivreDto>> ListarTimesSemDivisaoAsync(int temporada, CancellationToken ct)
    {
        var naTemporada = await _db.LigaTimes.AsNoTracking()
            .Where(lt => lt.Liga.Temporada == temporada && lt.Liga.Tipo == TipoCompetition.Liga && lt.Liga.Divisao != null)
            .Select(lt => lt.TimeId)
            .ToListAsync(ct);

        return await _db.Teams.AsNoTracking()
            .Where(t => !naTemporada.Contains(t.TeamId))
            .OrderBy(t => t.TeamName)
            .Select(t => new TemporadaTimeLivreDto(t.TeamId, t.TeamName, t.Roster.Count))
            .ToListAsync(ct);
    }

    private static LigaDto ToDto(Liga l) =>
        new(l.LigaId, l.Nome, l.TotalRodadas, l.DataInicio, l.DataFim, l.Status, l.Tipo, l.CriadoEm, l.AtualizadoEm,
            l.CampeaoTimeId, null, l.Temporada, l.Divisao, l.VagasDiretas, l.VagasPlayoff);
}
