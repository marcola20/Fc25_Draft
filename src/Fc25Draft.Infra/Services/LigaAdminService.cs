using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class LigaAdminService : ILigaAdminService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public LigaAdminService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    // ── Liga ─────────────────────────────────────────────────────────────────

    public async Task<LigaDto> CreateAsync(LigaCreateRequest request, CancellationToken ct)
    {
        await ValidarTemporadaDivisaoAsync(null, request.Tipo, request.Temporada, request.Divisao, ct);
        ValidarVagas(request.Divisao, request.VagasDiretas, request.VagasPlayoff);

        var now = _time.GetUtcNow().UtcDateTime;
        var liga = new Liga
        {
            LigaId = Guid.NewGuid(),
            Nome = request.Nome.Trim(),
            TotalRodadas = request.Tipo == TipoCompetition.Copa ? 6 : 8,
            DataInicio = request.DataInicio,
            DataFim = request.DataFim,
            Status = LigaStatus.Criada,
            Tipo = request.Tipo,
            Temporada = request.Temporada,
            Divisao = request.Divisao,
            VagasDiretas = request.Divisao is null ? null : request.VagasDiretas,
            VagasPlayoff = request.Divisao is null ? null : request.VagasPlayoff,
            CriadoEm = now,
            AtualizadoEm = now
        };

        _db.Ligas.Add(liga);
        await _db.SaveChangesAsync(ct);
        return ToDto(liga);
    }

    public async Task<LigaDto?> GetByIdAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().Include(x => x.Campeao)
            .FirstOrDefaultAsync(x => x.LigaId == ligaId, ct);
        return liga is null ? null : ToDto(liga);
    }

    public async Task<IReadOnlyList<LigaDto>> ListAsync(CancellationToken ct)
    {
        var list = await _db.Ligas.AsNoTracking().Include(x => x.Campeao)
            .OrderByDescending(x => x.CriadoEm).ToListAsync(ct);
        return list.Select(ToDto).ToArray();
    }

    public async Task<LigaDto> UpdateAsync(Guid ligaId, LigaUpdateRequest request, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        var temporada = request.Temporada ?? liga.Temporada;
        var divisao = request.RemoverDivisao ? null : request.Divisao ?? liga.Divisao;
        await ValidarTemporadaDivisaoAsync(liga.LigaId, liga.Tipo, temporada, divisao, ct);
        var vagasDiretas = divisao is null ? null : request.VagasDiretas ?? liga.VagasDiretas;
        var vagasPlayoff = divisao is null ? null : request.VagasPlayoff ?? liga.VagasPlayoff;
        ValidarVagas(divisao, vagasDiretas, vagasPlayoff);

        if (request.Nome is not null) liga.Nome = request.Nome.Trim();
        if (request.DataInicio.HasValue) liga.DataInicio = request.DataInicio.Value;
        if (request.DataFim.HasValue) liga.DataFim = request.DataFim.Value;
        liga.Temporada = temporada;
        liga.Divisao = divisao;
        liga.VagasDiretas = vagasDiretas;
        liga.VagasPlayoff = vagasPlayoff;
        liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
        return ToDto(liga);
    }

    private static void ValidarVagas(Divisao? divisao, int? vagasDiretas, int? vagasPlayoff)
    {
        if (vagasDiretas < 0 || vagasPlayoff < 0)
            throw new InvalidOperationException("As vagas de acesso/rebaixamento não podem ser negativas.");

        if (divisao is null && (vagasDiretas > 0 || vagasPlayoff > 0))
            throw new InvalidOperationException("Acesso e rebaixamento só existem em ligas com divisão (Série A ou B).");
    }

    private async Task ValidarTemporadaDivisaoAsync(Guid? ligaId, TipoCompetition tipo, int? temporada, Divisao? divisao, CancellationToken ct)
    {
        if (temporada is < 1900 or > 2999)
            throw new InvalidOperationException("Temporada deve ser um ano válido (ex.: 2010).");

        if (divisao is null) return;

        if (tipo != TipoCompetition.Liga)
            throw new InvalidOperationException("Só Ligas de pontos corridos têm divisão.");

        if (temporada is null)
            throw new InvalidOperationException("Informe a temporada para definir a divisão.");

        var ocupada = await _db.Ligas.AnyAsync(
            x => x.Temporada == temporada && x.Divisao == divisao && x.LigaId != ligaId, ct);
        if (ocupada)
            throw new InvalidOperationException(
                $"Já existe uma liga da {LigaLabels.DivisaoNome(divisao.Value)} na temporada {temporada}.");
    }

    public async Task<LigaDto> IniciarPrimeiraFaseAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (liga.Status != LigaStatus.Criada)
            throw new InvalidOperationException("Liga já iniciada.");

        liga.Status = LigaStatus.PrimeiraFase;
        liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;

        if (liga.Tipo == TipoCompetition.Copa)
            await IniciarPrimeiraFaseCopaAsync(liga, ct);
        else
            await IniciarPrimeiraFaseLigaAsync(liga, ct);

        await _db.SaveChangesAsync(ct);
        return ToDto(liga);
    }

    private async Task IniciarPrimeiraFaseLigaAsync(Liga liga, CancellationToken ct)
    {
        var timeIds = await _db.LigaTimes.AsNoTracking()
            .Where(x => x.LigaId == liga.LigaId)
            .Select(x => x.TimeId)
            .ToListAsync(ct);

        if (timeIds.Count < 2)
            throw new InvalidOperationException("Configure os times participantes na aba \"Times\" antes de iniciar a liga.");

        foreach (var timeId in timeIds)
        {
            if (!await _db.LigaClassificacoes.AnyAsync(x => x.LigaId == liga.LigaId && x.TimeId == timeId, ct))
            {
                _db.LigaClassificacoes.Add(new LigaClassificacao
                {
                    ClassificacaoId = Guid.NewGuid(),
                    LigaId = liga.LigaId,
                    TimeId = timeId
                });
            }
        }
    }

    private async Task IniciarPrimeiraFaseCopaAsync(Liga liga, CancellationToken ct)
    {
        var grupos = await _db.LigaGruposTimes
            .AsNoTracking()
            .Where(x => x.LigaId == liga.LigaId)
            .Include(x => x.Time)
            .ToListAsync(ct);

        var porGrupo = grupos
            .GroupBy(x => x.Grupo)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Select(x => x.TimeId).ToList());

        if (porGrupo.Count < 2 || porGrupo.Values.Any(t => t.Count < 2))
            throw new InvalidOperationException("A Copa precisa de pelo menos 2 grupos, com 2 times ou mais em cada.");

        // Classificação com grupo definido
        foreach (var g in grupos)
        {
            if (!await _db.LigaClassificacoes.AnyAsync(x => x.LigaId == liga.LigaId && x.TimeId == g.TimeId, ct))
            {
                _db.LigaClassificacoes.Add(new LigaClassificacao
                {
                    ClassificacaoId = Guid.NewGuid(),
                    LigaId = liga.LigaId,
                    TimeId = g.TimeId,
                    Grupo = g.Grupo
                });
            }
        }

        // Rodadas existentes?
        var jaTemRodadas = await _db.LigaRodadas.AnyAsync(x => x.LigaId == liga.LigaId, ct);
        if (jaTemRodadas) return;

        // Fase de grupos: cada time enfrenta os do próprio grupo (grupo de 4 = 3 rodadas).
        var jogos = GerarRodadasDosGrupos(porGrupo.Values.ToList());
        liga.TotalRodadas = jogos.Count;

        // As sextas do calendário da temporada.
        var datasDaCopa = await DatasDoCalendarioAsync(liga, ct);

        for (int r = 0; r < jogos.Count; r++)
        {
            var rodada = new LigaRodada
            {
                RodadaId = Guid.NewGuid(),
                LigaId = liga.LigaId,
                Numero = r + 1,
                DataHora = r < datasDaCopa.Count ? datasDaCopa[r] : null
            };
            foreach (var (casa, fora) in jogos[r])
            {
                rodada.Partidas.Add(new LigaPartida
                {
                    PartidaId = Guid.NewGuid(),
                    TimeCasaId = casa,
                    TimeForaId = fora,
                    Status = PartidaStatus.Agendada
                });
            }
            _db.LigaRodadas.Add(rodada);
        }
    }

    /// <summary>
    /// Rodadas da fase de grupos: round-robin dentro de cada grupo, com os jogos de todos os
    /// grupos acontecendo na mesma rodada. Grupos menores simplesmente acabam antes.
    /// A fase dura o tanto de jogos que cada time faz (num grupo de N, N-1 rodadas): no grupo
    /// ímpar os jogos que sobrariam numa rodada extra entram na última, e ali alguns times jogam
    /// duas vezes — assim a Copa não ganha datas só para um grupo ou outro.
    /// </summary>
    private static List<List<(Guid, Guid)>> GerarRodadasDosGrupos(List<List<Guid>> grupos)
    {
        var porGrupo = grupos
            .Select(times =>
            {
                // Grupo ímpar sai do sorteio com uma rodada a mais (um time folga por rodada).
                var doGrupo = GerarRoundRobinParcial(times, times.Count % 2 == 0 ? times.Count - 1 : times.Count);

                if (times.Count % 2 != 0 && doGrupo.Count > 1)
                {
                    doGrupo[^2].AddRange(doGrupo[^1]);
                    doGrupo.RemoveAt(doGrupo.Count - 1);
                }

                return doGrupo;
            })
            .ToList();

        var totalRodadas = porGrupo.Max(g => g.Count);
        var rodadas = new List<List<(Guid, Guid)>>(totalRodadas);

        for (int r = 0; r < totalRodadas; r++)
        {
            var rodada = new List<(Guid, Guid)>();
            foreach (var grupo in porGrupo.Where(g => r < g.Count))
                rodada.AddRange(grupo[r]);

            rodadas.Add(rodada);
        }

        return rodadas;
    }


    public async Task<LigaDto> EncerrarPrimeiraFaseAsync(Guid ligaId, CancellationToken ct)
    {
        Liga liga = null!;
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            liga = await EncerrarPrimeiraFaseCoreAsync(ligaId, ct);
            await tx.CommitAsync(ct);
        });
        return ToDto(liga);
    }

    private async Task<Liga> EncerrarPrimeiraFaseCoreAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (liga.Status != LigaStatus.PrimeiraFase)
            throw new InvalidOperationException("Liga não está na primeira fase.");

        if (liga.Tipo == TipoCompetition.Supercopa)
        {
            // Jogo único: o campeão sai do placar (ou dos pênaltis), sem passar pela classificação.
            var decisao = await _db.LigaPartidas.AsNoTracking()
                .Where(p => p.Rodada.LigaId == ligaId)
                .OrderBy(p => p.Rodada.Numero)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("A Supercopa não tem partida cadastrada.");

            if (decisao.Status != PartidaStatus.Encerrada)
                throw new InvalidOperationException("Encerre a partida da Supercopa antes de encerrar a competição.");

            var campeao = LigaDesempate.VencedorDoJogoDecisivo(
                decisao.TimeCasaId, decisao.TimeForaId, decisao.GolsCasa, decisao.GolsFora,
                decisao.TemPenaltis, decisao.PenaltisVencedorId)
                ?? throw new InvalidOperationException(
                    "A Supercopa terminou empatada. Registre o vencedor nos pênaltis antes de encerrar.");

            liga.Status = LigaStatus.Encerrada;
            liga.CampeaoTimeId = campeao;
        }
        else if (liga.Tipo == TipoCompetition.Copa)
        {
            // Empate sem jogo decisivo no topo do grupo deixaria a semifinal indefinida.
            await GarantirDesempatesDaCopaAsync(ligaId, ct);

            // Copa vai direto para PlayIn (knockout 4 times)
            liga.Status = LigaStatus.PlayIn;
        }
        else
        {
            // Liga (pontos corridos): o campeão é o 1º colocado. Só há fase extra em caso
            // de empate no topo — jogo decisivo (2 times) ou mini liga (3+). Sem empate,
            // a competição encerra direto no líder (não há playoffs no formato da Liga).
            // Com acesso/rebaixamento, empate total numa posição de zona precisa estar resolvido antes.
            await GarantirDesempatesDeZonaAsync(liga, ct);

            var classif = await _db.LigaClassificacoes
                .AsNoTracking()
                .Where(x => x.LigaId == ligaId)
                .OrderByDescending(x => x.Pontos)
                .ThenByDescending(x => x.Vitorias)
                .ThenByDescending(x => x.GolsPro - x.GolsContra)
                .ThenByDescending(x => x.GolsPro)
                .ToListAsync(ct);

            if (classif.Count >= 2)
            {
                var lider = classif[0];
                // O título não sai em V/SG/GP: quem empatar em pontos com o líder
                // disputa jogo decisivo (2 times) ou mini liga (3+).
                var empatados = classif
                    .Where(c => c.Pontos == lider.Pontos)
                    .ToList();

                if (empatados.Count >= 3)
                {
                    liga.Status = LigaStatus.MiniLiga;
                }
                else if (empatados.Count == 2)
                {
                    liga.Status = LigaStatus.DecisaoCampeao;
                }
                else
                {
                    // Líder isolado → campeão definido.
                    liga.Status = LigaStatus.Encerrada;
                    liga.CampeaoTimeId = lider.TimeId;
                }
            }
            else
            {
                // 0 ou 1 time classificado: não há empate possível, encerra direto.
                liga.Status = LigaStatus.Encerrada;
                liga.CampeaoTimeId = classif.FirstOrDefault()?.TimeId;
            }
        }

        liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        // Gera o mata-mata automaticamente quando a fase encerra em PlayIn (Copa → semis/final).
        if (liga.Status == LigaStatus.PlayIn)
        {
            var jaExiste = await _db.LigaKnockoutJogos.AnyAsync(x => x.LigaId == ligaId, ct);
            if (!jaExiste)
                await GerarFaseKnockoutAsync(ligaId, ct);
        }

        return liga;
    }

    public async Task<LigaDto> ReverterParaPrimeiraFaseAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (liga.Status is not (LigaStatus.PlayIn or LigaStatus.Playoffs
            or LigaStatus.DecisaoCampeao or LigaStatus.MiniLiga))
            throw new InvalidOperationException(
                "Só é possível reverter uma liga que esteja em Play-In, Playoffs, Decisão Campeão ou Mini Liga.");

        // Remove apenas o que é gerado DEPOIS da 1ª fase (mata-mata e rodadas de desempate da Liga:
        // mini liga com Numero=0 e jogo decisivo com Numero=-1), preservando as rodadas regulares.
        var knockouts = await _db.LigaKnockoutJogos.Where(x => x.LigaId == ligaId).ToListAsync(ct);
        // O jogo decisivo da Copa (Desempate) é disputado ainda na fase de grupos, então fica:
        // apagá-lo desfaria o desempate e bloquearia o reencerramento da fase.
        var miniRodadas = await _db.LigaRodadas
            .Where(x => x.LigaId == ligaId && x.Numero <= 0)
            .ToListAsync(ct);
        var miniRodadaIds = miniRodadas.Select(r => r.RodadaId).ToList();
        var miniPartidas = await _db.LigaPartidas.Where(x => miniRodadaIds.Contains(x.RodadaId)).ToListAsync(ct);
        var miniPartidaIds = miniPartidas.Select(p => p.PartidaId).ToList();
        var miniEventos = await _db.LigaEventos.Where(x => miniPartidaIds.Contains(x.PartidaId)).ToListAsync(ct);

        _db.LigaEventos.RemoveRange(miniEventos);
        _db.LigaPartidas.RemoveRange(miniPartidas);
        _db.LigaRodadas.RemoveRange(miniRodadas);
        _db.LigaKnockoutJogos.RemoveRange(knockouts);

        liga.Status = LigaStatus.PrimeiraFase;
        liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);
        await RecalcularPosicoesAsync(ligaId, ct); // o jogo de título apagado deixa de mexer na ordem
        return ToDto(liga);
    }

    public async Task DeleteLigaAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        // Delete all related entities (cascade)
        var rodadas = await _db.LigaRodadas.Where(x => x.LigaId == ligaId).ToListAsync(ct);
        var partidas = await _db.LigaPartidas.Where(x => x.Rodada.LigaId == ligaId).ToListAsync(ct);
        var eventos = await _db.LigaEventos.Where(x => x.Partida.Rodada.LigaId == ligaId).ToListAsync(ct);
        var knockouts = await _db.LigaKnockoutJogos.Where(x => x.LigaId == ligaId).ToListAsync(ct);
        var classif = await _db.LigaClassificacoes.Where(x => x.LigaId == ligaId).ToListAsync(ct);
        var punicoes = await _db.LigaPunicoes.Where(x => x.LigaId == ligaId).ToListAsync(ct);
        var grupos = await _db.LigaGruposTimes.Where(x => x.LigaId == ligaId).ToListAsync(ct);
        var timesInscritos = await _db.LigaTimes.Where(x => x.LigaId == ligaId).ToListAsync(ct);

        _db.LigaEventos.RemoveRange(eventos);
        _db.LigaPartidas.RemoveRange(partidas);
        _db.LigaRodadas.RemoveRange(rodadas);
        _db.LigaKnockoutJogos.RemoveRange(knockouts);
        _db.LigaClassificacoes.RemoveRange(classif);
        _db.LigaPunicoes.RemoveRange(punicoes);
        _db.LigaGruposTimes.RemoveRange(grupos);
        _db.LigaTimes.RemoveRange(timesInscritos);
        _db.Ligas.Remove(liga);

        await _db.SaveChangesAsync(ct);
    }

    // ── Rodadas ───────────────────────────────────────────────────────────────

    public async Task<LigaRodadaDto> CreateRodadaAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        var existentes = await _db.LigaRodadas.CountAsync(x => x.LigaId == ligaId, ct);
        if (existentes >= liga.TotalRodadas)
            throw new InvalidOperationException($"Limite de {liga.TotalRodadas} rodadas atingido.");

        var rodada = new LigaRodada
        {
            RodadaId = Guid.NewGuid(),
            LigaId = ligaId,
            Numero = existentes + 1
        };

        _db.LigaRodadas.Add(rodada);
        await _db.SaveChangesAsync(ct);
        return new LigaRodadaDto(rodada.RodadaId, rodada.LigaId, rodada.Numero, 0);
    }

    public async Task<IReadOnlyList<LigaRodadaDto>> ListRodadasAsync(Guid ligaId, CancellationToken ct)
    {
        var rodadas = await _db.LigaRodadas
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .OrderBy(x => x.Numero)
            .Select(x => new { x.RodadaId, x.LigaId, x.Numero, x.Desempate, x.DataHora, Total = x.Partidas.Count })
            .ToListAsync(ct);

        return rodadas.Select(r => new LigaRodadaDto(r.RodadaId, r.LigaId, r.Numero, r.Total, r.Desempate, r.DataHora)).ToArray();
    }

    /// <summary>
    /// Marca as rodadas com as datas do calendário da temporada (domingo da Supercopa, terça da
    /// Série B, quarta da Série A e sexta da Copa). A Supercopa, que é jogo único, fica na abertura.
    /// Retorna quantas rodadas foram marcadas.
    /// </summary>
    public async Task<int> AplicarCalendarioAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        var rodadas = await _db.LigaRodadas
            .Where(r => r.LigaId == ligaId && r.Numero > 0 && !r.Desempate)
            .OrderBy(r => r.Numero)
            .ToListAsync(ct);

        if (rodadas.Count == 0)
            throw new InvalidOperationException("Gere as rodadas antes de aplicar o calendário.");

        var datas = await DatasDoCalendarioAsync(liga, ct);
        if (datas.Count == 0)
            throw new InvalidOperationException("O calendário da temporada não tem datas para esta competição.");

        var marcadas = 0;
        for (int i = 0; i < rodadas.Count && i < datas.Count; i++)
        {
            rodadas[i].DataHora = datas[i];
            marcadas++;
        }

        await _db.SaveChangesAsync(ct);
        return marcadas;
    }

    /// <summary>Datas do calendário para esta competição, na ordem das rodadas.</summary>
    private async Task<IReadOnlyList<DateTime>> DatasDoCalendarioAsync(Liga liga, CancellationToken ct)
    {
        if (liga.Temporada is not int temporada) return Array.Empty<DateTime>();

        var rodadasSerieA = await ContarTimesDaDivisaoAsync(temporada, Divisao.SerieA, ct);
        var rodadasSerieB = await ContarTimesDaDivisaoAsync(temporada, Divisao.SerieB, ct);
        var rodadasGrupoCopa = await _db.LigaRodadas
            .CountAsync(r => r.Liga.Temporada == temporada && r.Liga.Tipo == TipoCompetition.Copa && r.Numero > 0 && !r.Desempate, ct);

        var calendario = CalendarioTemporada.Montar(
            CalendarioTemporada.Abertura,
            rodadasSerieA > 0 ? rodadasSerieA : 9,
            rodadasSerieB,
            rodadasGrupoCopa > 0 ? rodadasGrupoCopa : 4);

        // Supercopa é jogo único: fica na abertura.
        if (liga.Tipo == TipoCompetition.Supercopa)
            return new[] { calendario[0].Quando };

        return CalendarioTemporada.DatasDasRodadas(calendario, liga.Tipo, liga.Divisao);
    }

    /// <summary>Rodadas de uma divisão = times - 1 (turno único).</summary>
    private async Task<int> ContarTimesDaDivisaoAsync(int temporada, Divisao divisao, CancellationToken ct)
    {
        var times = await _db.LigaTimes
            .CountAsync(t => t.Liga.Temporada == temporada && t.Liga.Tipo == TipoCompetition.Liga && t.Liga.Divisao == divisao, ct);

        return times > 1 ? times - 1 : 0;
    }

    public async Task<IReadOnlyList<LigaRodadaDto>> GerarRodadasAutoAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        var existentes = await _db.LigaRodadas.CountAsync(x => x.LigaId == ligaId, ct);
        if (existentes > 0)
            throw new InvalidOperationException("Liga já possui rodadas. Delete-as antes de gerar automaticamente.");

        List<Guid> times;
        if (liga.Tipo == TipoCompetition.Liga)
        {
            times = await _db.LigaTimes.AsNoTracking()
                .Where(x => x.LigaId == ligaId)
                .Select(x => x.TimeId)
                .ToListAsync(ct);
            if (times.Count < 2)
                throw new InvalidOperationException("Configure os times participantes na aba \"Times\" antes de gerar as rodadas.");
        }
        else
        {
            times = await _db.Teams.AsNoTracking().Select(t => t.TeamId).ToListAsync(ct);
            if (times.Count < 2)
                throw new InvalidOperationException("Precisa de ao menos 2 times para gerar rodadas.");
        }

        // Liga: round-robin completo (n-1 rodadas). Copa: usa TotalRodadas (6).
        var totalRodadas = liga.Tipo == TipoCompetition.Liga ? times.Count - 1 : liga.TotalRodadas;
        var jogos = GerarRoundRobinParcial(times, totalRodadas);

        PorConfrontoFinal(jogos, liga.ConfrontoFinalTimeAId, liga.ConfrontoFinalTimeBId);

        var rodadasCriadas = new List<LigaRodada>();
        for (int r = 0; r < totalRodadas; r++)
        {
            var rodada = new LigaRodada
            {
                RodadaId = Guid.NewGuid(),
                LigaId = ligaId,
                Numero = r + 1
            };

            foreach (var (casa, fora) in jogos[r])
            {
                rodada.Partidas.Add(new LigaPartida
                {
                    PartidaId = Guid.NewGuid(),
                    TimeCasaId = casa,
                    TimeForaId = fora,
                    Status = PartidaStatus.Agendada
                });
            }

            _db.LigaRodadas.Add(rodada);
            rodadasCriadas.Add(rodada);
        }

        // Já nascem marcadas com a data do calendário da temporada.
        var datasDoCalendario = await DatasDoCalendarioAsync(liga, ct);
        for (int i = 0; i < rodadasCriadas.Count && i < datasDoCalendario.Count; i++)
            rodadasCriadas[i].DataHora = datasDoCalendario[i];

        await _db.SaveChangesAsync(ct);

        return rodadasCriadas.Select(r => new LigaRodadaDto(r.RodadaId, r.LigaId, r.Numero, r.Partidas.Count, false, r.DataHora)).ToArray();
    }

    public async Task DeleteRodadaAsync(Guid rodadaId, CancellationToken ct)
    {
        var rodada = await _db.LigaRodadas.FirstOrDefaultAsync(x => x.RodadaId == rodadaId, ct)
            ?? throw new InvalidOperationException("Rodada não encontrada.");

        _db.LigaRodadas.Remove(rodada);
        await _db.SaveChangesAsync(ct);
    }

    // ── Partidas ──────────────────────────────────────────────────────────────

    public async Task<LigaPartidaDto?> GetPartidaByIdAsync(Guid partidaId, CancellationToken ct)
    {
        var exists = await _db.LigaPartidas.AnyAsync(x => x.PartidaId == partidaId, ct);
        return exists ? await GetPartidaDtoAsync(partidaId, ct) : null;
    }

    public async Task<LigaPartidaDto> CreatePartidaAsync(Guid rodadaId, LigaPartidaCreateRequest request, CancellationToken ct)
    {
        var rodada = await _db.LigaRodadas.AsNoTracking().FirstOrDefaultAsync(x => x.RodadaId == rodadaId, ct)
            ?? throw new InvalidOperationException("Rodada não encontrada.");

        var partida = new LigaPartida
        {
            PartidaId = Guid.NewGuid(),
            RodadaId = rodadaId,
            TimeCasaId = request.TimeCasaId,
            TimeForaId = request.TimeForaId,
            Status = PartidaStatus.Agendada
        };

        _db.LigaPartidas.Add(partida);
        await _db.SaveChangesAsync(ct);

        return await GetPartidaDtoAsync(partida.PartidaId, ct);
    }

    public async Task<IReadOnlyList<LigaPartidaDto>> ListPartidasAsync(Guid rodadaId, CancellationToken ct)
    {
        var partidas = await _db.LigaPartidas
            .AsNoTracking()
            .Where(x => x.RodadaId == rodadaId)
            .Include(x => x.Rodada)
            .Include(x => x.TimeCasa)
            .Include(x => x.TimeFora)
            .ToListAsync(ct);

        return partidas.Select(ToPartidaDto).ToArray();
    }

    public async Task<LigaPartidaDto> IniciarPartidaAsync(Guid partidaId, CancellationToken ct)
    {
        var partida = await _db.LigaPartidas.FirstOrDefaultAsync(x => x.PartidaId == partidaId, ct)
            ?? throw new InvalidOperationException("Partida não encontrada.");

        if (partida.Status != PartidaStatus.Agendada)
            throw new InvalidOperationException("Partida já iniciada ou encerrada.");

        partida.Status = PartidaStatus.EmAndamento;
        partida.IniciadaEm = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        await RecalcularClassificacaoAsync(partida.RodadaId, ct);
        return await GetPartidaDtoAsync(partidaId, ct);
    }

    public async Task<LigaPartidaDto> EncerrarPartidaAsync(Guid partidaId, CancellationToken ct)
    {
        var partida = await _db.LigaPartidas.FirstOrDefaultAsync(x => x.PartidaId == partidaId, ct)
            ?? throw new InvalidOperationException("Partida não encontrada.");

        if (partida.Status != PartidaStatus.EmAndamento)
            throw new InvalidOperationException("Partida não está em andamento.");

        partida.Status = PartidaStatus.Encerrada;
        partida.EncerradaEm = _time.GetUtcNow().UtcDateTime;
        await RegistrarEscalacoesAsync(partida, ct);
        await _db.SaveChangesAsync(ct);

        await RecalcularClassificacaoAsync(partida.RodadaId, ct);
        return await GetPartidaDtoAsync(partidaId, ct);
    }

    public async Task<LigaPartidaDto> EncerrarPartidaComPenaltisAsync(Guid partidaId, Guid vencedorId, CancellationToken ct)
    {
        var partida = await _db.LigaPartidas.FirstOrDefaultAsync(x => x.PartidaId == partidaId, ct)
            ?? throw new InvalidOperationException("Partida não encontrada.");

        if (partida.Status != PartidaStatus.EmAndamento)
            throw new InvalidOperationException("Partida não está em andamento.");

        if (partida.GolsCasa != partida.GolsFora)
            throw new InvalidOperationException("Pênaltis só se aplicam a jogos empatados no tempo normal.");

        if (vencedorId != partida.TimeCasaId && vencedorId != partida.TimeForaId)
            throw new InvalidOperationException("O vencedor dos pênaltis deve ser um dos times da partida.");

        partida.TemPenaltis = true;
        partida.PenaltisVencedorId = vencedorId;
        partida.Status = PartidaStatus.Encerrada;
        partida.EncerradaEm = _time.GetUtcNow().UtcDateTime;
        await RegistrarEscalacoesAsync(partida, ct);
        await _db.SaveChangesAsync(ct);

        await RecalcularClassificacaoAsync(partida.RodadaId, ct);
        return await GetPartidaDtoAsync(partidaId, ct);
    }

    public async Task<LigaPartidaDto> AplicarWOAsync(Guid partidaId, Guid timeWOId, CancellationToken ct)
    {
        var partida = await _db.LigaPartidas.FirstOrDefaultAsync(x => x.PartidaId == partidaId, ct)
            ?? throw new InvalidOperationException("Partida não encontrada.");

        if (partida.Status == PartidaStatus.Encerrada)
            throw new InvalidOperationException("Partida já encerrada.");

        if (partida.TimeCasaId != timeWOId && partida.TimeForaId != timeWOId)
            throw new InvalidOperationException("Time não participa desta partida.");

        // W.O.: time que fez WO perde 2x0
        bool casaFezWO = partida.TimeCasaId == timeWOId;
        partida.GolsCasa = casaFezWO ? 0 : 2;
        partida.GolsFora = casaFezWO ? 2 : 0;
        partida.IsWO = true;
        partida.Status = PartidaStatus.Encerrada;
        partida.IniciadaEm ??= _time.GetUtcNow().UtcDateTime;
        partida.EncerradaEm = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
        await RecalcularClassificacaoAsync(partida.RodadaId, ct);

        // Integração com o mata-mata: se esta partida pertence a um jogo do bracket,
        // resolve o vencedor (quem não fez W.O.) e avança automaticamente.
        var jogoKo = await _db.LigaKnockoutJogos
            .FirstOrDefaultAsync(x => x.PartidaId == partidaId && x.VencedorId == null, ct);
        if (jogoKo is not null && jogoKo.TimeCasaId.HasValue && jogoKo.TimeForaId.HasValue)
        {
            var vencedorId = casaFezWO ? partida.TimeForaId : partida.TimeCasaId;
            jogoKo.VencedorId = vencedorId;
            await _db.SaveChangesAsync(ct);
            await AvancarBracketAsync(jogoKo, vencedorId, ct);
        }

        return await GetPartidaDtoAsync(partidaId, ct);
    }

    public async Task DeletePartidaAsync(Guid partidaId, CancellationToken ct)
    {
        var partida = await _db.LigaPartidas.FirstOrDefaultAsync(x => x.PartidaId == partidaId, ct)
            ?? throw new InvalidOperationException("Partida não encontrada.");

        _db.LigaPartidas.Remove(partida);
        await _db.SaveChangesAsync(ct);
        await RecalcularClassificacaoAsync(partida.RodadaId, ct);
    }

    // ── Eventos ───────────────────────────────────────────────────────────────

    public async Task<LigaEventoDto> AddGolAsync(Guid partidaId, LigaGolRequest request, CancellationToken ct)
    {
        var partida = await _db.LigaPartidas.FirstOrDefaultAsync(x => x.PartidaId == partidaId, ct)
            ?? throw new InvalidOperationException("Partida não encontrada.");

        if (partida.Status == PartidaStatus.Agendada)
            throw new InvalidOperationException("Inicie a partida antes de registrar eventos.");

        if (partida.TimeCasaId != request.TimeId && partida.TimeForaId != request.TimeId)
            throw new InvalidOperationException("Time não participa desta partida.");

        if (!await _db.Players.AnyAsync(p => p.PlayerId == request.JogadorId, ct))
            throw new InvalidOperationException("Jogador não encontrado.");

        if (!request.GolContra && request.AssistenteId.HasValue &&
            !await _db.Players.AnyAsync(p => p.PlayerId == request.AssistenteId.Value, ct))
            throw new InvalidOperationException("Assistente não encontrado.");

        var evento = new LigaEventoPartida
        {
            EventoId = Guid.NewGuid(),
            PartidaId = partidaId,
            Tipo = request.GolContra ? TipoEvento.GolContra : TipoEvento.Gol,
            TimeId = request.TimeId,
            JogadorId = request.JogadorId,
            AssistenteId = request.GolContra ? null : request.AssistenteId,
            Minuto = request.Minuto,
            CriadoEm = _time.GetUtcNow().UtcDateTime
        };

        _db.LigaEventos.Add(evento);

        // Atualiza placar
        if (request.TimeId == partida.TimeCasaId)
            partida.GolsCasa++;
        else
            partida.GolsFora++;

        await _db.SaveChangesAsync(ct);
        await RecalcularClassificacaoAsync(partida.RodadaId, ct);

        return await GetEventoDtoAsync(evento.EventoId, ct);
    }

    public async Task<LigaEventoDto> AddCartaoAsync(Guid partidaId, LigaCartaoRequest request, CancellationToken ct)
    {
        if (request.Tipo != TipoEvento.CartaoAmarelo && request.Tipo != TipoEvento.CartaoVermelho)
            throw new InvalidOperationException("Tipo inválido para cartão.");

        var partida = await _db.LigaPartidas.FirstOrDefaultAsync(x => x.PartidaId == partidaId, ct)
            ?? throw new InvalidOperationException("Partida não encontrada.");

        if (partida.Status == PartidaStatus.Agendada)
            throw new InvalidOperationException("Inicie a partida antes de registrar eventos.");

        if (partida.TimeCasaId != request.TimeId && partida.TimeForaId != request.TimeId)
            throw new InvalidOperationException("Time não participa desta partida.");

        if (!await _db.Players.AnyAsync(p => p.PlayerId == request.JogadorId, ct))
            throw new InvalidOperationException("Jogador não encontrado.");

        var evento = new LigaEventoPartida
        {
            EventoId = Guid.NewGuid(),
            PartidaId = partidaId,
            Tipo = request.Tipo,
            TimeId = request.TimeId,
            JogadorId = request.JogadorId,
            Minuto = request.Minuto,
            CriadoEm = _time.GetUtcNow().UtcDateTime
        };

        _db.LigaEventos.Add(evento);
        await _db.SaveChangesAsync(ct);
        await RecalcularClassificacaoAsync(partida.RodadaId, ct);

        return await GetEventoDtoAsync(evento.EventoId, ct);
    }

    public async Task<LigaEventoDto> AddSubstituicaoAsync(Guid partidaId, LigaSubstituicaoRequest request, CancellationToken ct)
    {
        var partida = await _db.LigaPartidas.FirstOrDefaultAsync(x => x.PartidaId == partidaId, ct)
            ?? throw new InvalidOperationException("Partida não encontrada.");

        if (partida.Status == PartidaStatus.Agendada)
            throw new InvalidOperationException("Inicie a partida antes de registrar eventos.");

        if (partida.TimeCasaId != request.TimeId && partida.TimeForaId != request.TimeId)
            throw new InvalidOperationException("Time não participa desta partida.");

        if (!await _db.Players.AnyAsync(p => p.PlayerId == request.JogadorId, ct))
            throw new InvalidOperationException("Jogador não encontrado.");

        if (request.JogadorSaiuId == request.JogadorId)
            throw new InvalidOperationException("Quem entra e quem sai precisam ser jogadores diferentes.");

        if (request.JogadorSaiuId.HasValue && !await _db.Players.AnyAsync(p => p.PlayerId == request.JogadorSaiuId.Value, ct))
            throw new InvalidOperationException("Jogador que saiu não encontrado.");

        // Quem está em campo agora = titulares + quem já entrou − quem já saiu.
        var escalacao = (await EscalacaoPartidaLoader.CarregarAsync(_db, partida, ct))
            .Where(l => l.TimeId == request.TimeId)
            .ToList();
        var subs = await _db.LigaEventos
            .AsNoTracking()
            .Where(e => e.PartidaId == partidaId && e.TimeId == request.TimeId && e.Tipo == TipoEvento.Substituicao)
            .Select(e => new { e.JogadorId, e.JogadorSaiuId })
            .ToListAsync(ct);
        var entraram = subs.Select(s => s.JogadorId).ToHashSet();
        var sairam = subs.Where(s => s.JogadorSaiuId.HasValue).Select(s => s.JogadorSaiuId!.Value).ToHashSet();
        var emCampo = escalacao.Where(l => l.Titular).Select(l => l.JogadorId)
            .Concat(entraram)
            .Where(id => !sairam.Contains(id))
            .ToHashSet();

        if (sairam.Contains(request.JogadorId))
            throw new InvalidOperationException("Este jogador já foi substituído e não pode voltar.");
        if (emCampo.Contains(request.JogadorId))
            throw new InvalidOperationException("Este jogador já está em campo.");
        if (request.JogadorSaiuId is int saiuId)
        {
            if (sairam.Contains(saiuId))
                throw new InvalidOperationException("Este jogador já saiu de campo.");
            // Sem escalação registrada não dá para conferir quem estava em campo.
            if (escalacao.Count > 0 && !emCampo.Contains(saiuId))
                throw new InvalidOperationException("O jogador que saiu não está em campo.");
        }

        var evento = new LigaEventoPartida
        {
            EventoId = Guid.NewGuid(),
            PartidaId = partidaId,
            Tipo = TipoEvento.Substituicao,
            TimeId = request.TimeId,
            JogadorId = request.JogadorId,
            JogadorSaiuId = request.JogadorSaiuId,
            Minuto = request.Minuto,
            CriadoEm = _time.GetUtcNow().UtcDateTime
        };

        _db.LigaEventos.Add(evento);
        await _db.SaveChangesAsync(ct);

        return await GetEventoDtoAsync(evento.EventoId, ct);
    }

    /// <summary>
    /// Tira o retrato da escalação ativa (titulares e banco) de cada time ao
    /// encerrar a partida — os titulares ganham +1 jogo. Só grava na primeira
    /// vez: reencerrar não sobrescreve o retrato original.
    /// </summary>
    private async Task RegistrarEscalacoesAsync(LigaPartida partida, CancellationToken ct)
    {
        if (partida.IsWO) return;
        if (await _db.LigaEscalacoes.AnyAsync(x => x.PartidaId == partida.PartidaId, ct)) return;

        var linhas = await EscalacaoPartidaLoader.EscalacaoAtivaAsync(
            _db, new[] { partida.TimeCasaId, partida.TimeForaId }, ct);

        _db.LigaEscalacoes.AddRange(linhas.Select(l => new LigaEscalacaoPartida
        {
            Id = Guid.NewGuid(),
            PartidaId = partida.PartidaId,
            TimeId = l.TimeId,
            JogadorId = l.JogadorId,
            Titular = l.Titular,
            Ordem = l.Ordem
        }));
    }

    public async Task DeleteEventoAsync(Guid eventoId, CancellationToken ct)
    {
        var evento = await _db.LigaEventos
            .Include(x => x.Partida)
            .FirstOrDefaultAsync(x => x.EventoId == eventoId, ct)
            ?? throw new InvalidOperationException("Evento não encontrado.");

        var partida = evento.Partida;

        if (evento.Tipo == TipoEvento.Gol || evento.Tipo == TipoEvento.GolContra)
        {
            if (evento.TimeId == partida.TimeCasaId && partida.GolsCasa > 0)
                partida.GolsCasa--;
            else if (evento.TimeId == partida.TimeForaId && partida.GolsFora > 0)
                partida.GolsFora--;
        }

        _db.LigaEventos.Remove(evento);
        await _db.SaveChangesAsync(ct);
        await RecalcularClassificacaoAsync(partida.RodadaId, ct);
    }

    public async Task<IReadOnlyList<LigaEventoDto>> ListEventosAsync(Guid partidaId, CancellationToken ct)
    {
        var eventos = await _db.LigaEventos
            .AsNoTracking()
            .Where(x => x.PartidaId == partidaId)
            .Include(x => x.Time)
            .Include(x => x.Jogador)
            .Include(x => x.Assistente)
            .Include(x => x.JogadorSaiu)
            .OrderBy(x => x.Minuto)
            .ThenBy(x => x.CriadoEm)
            .ToListAsync(ct);

        return eventos.Select(ToEventoDto).ToArray();
    }

    // ── Punições ──────────────────────────────────────────────────────────────

    public async Task<LigaPunicaoDto> AplicarPunicaoAsync(Guid ligaId, LigaPunicaoRequest request, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        var time = await _db.Teams.AsNoTracking().FirstOrDefaultAsync(x => x.TeamId == request.TimeId, ct)
            ?? throw new InvalidOperationException("Time não encontrado.");

        LigaPunicao punicao = null!;
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            punicao = new LigaPunicao
            {
                PunicaoId = Guid.NewGuid(),
                LigaId = ligaId,
                TimeId = request.TimeId,
                PontosSubtraidos = request.PontosSubtraidos,
                Motivo = request.Motivo.Trim(),
                CriadaEm = _time.GetUtcNow().UtcDateTime
            };

            _db.LigaPunicoes.Add(punicao);
            await _db.SaveChangesAsync(ct);

            // Aplica desconto na classificação (atômico com o registro da punição)
            var classif = await _db.LigaClassificacoes.FirstOrDefaultAsync(x => x.LigaId == ligaId && x.TimeId == request.TimeId, ct);
            if (classif is not null)
            {
                classif.Pontos = classif.Pontos - request.PontosSubtraidos;
                await _db.SaveChangesAsync(ct);
                await RecalcularPosicoesAsync(ligaId, ct);
            }

            await tx.CommitAsync(ct);
        });

        return new LigaPunicaoDto(punicao.PunicaoId, ligaId, request.TimeId, time.TeamName, request.PontosSubtraidos, punicao.Motivo, punicao.CriadaEm);
    }

    public async Task<IReadOnlyList<LigaPunicaoDto>> ListPunicoesAsync(Guid ligaId, CancellationToken ct)
    {
        var punicoes = await _db.LigaPunicoes
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .Include(x => x.Time)
            .OrderByDescending(x => x.CriadaEm)
            .ToListAsync(ct);

        return punicoes.Select(p => new LigaPunicaoDto(p.PunicaoId, p.LigaId, p.TimeId, p.Time.TeamName, p.PontosSubtraidos, p.Motivo, p.CriadaEm)).ToArray();
    }

    public async Task RemoverPunicaoAsync(Guid punicaoId, CancellationToken ct)
    {
        var punicao = await _db.LigaPunicoes.FirstOrDefaultAsync(x => x.PunicaoId == punicaoId, ct)
            ?? throw new InvalidOperationException("Punição não encontrada.");

        var ligaId = punicao.LigaId;
        var timeId = punicao.TimeId;
        var pontos = punicao.PontosSubtraidos;

        _db.LigaPunicoes.Remove(punicao);
        await _db.SaveChangesAsync(ct);

        // Devolve pontos
        var classif = await _db.LigaClassificacoes.FirstOrDefaultAsync(x => x.LigaId == ligaId && x.TimeId == timeId, ct);
        if (classif is not null)
        {
            classif.Pontos += pontos;
            await _db.SaveChangesAsync(ct);
            await RecalcularPosicoesAsync(ligaId, ct);
        }
    }

    // ── Knockout ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<LigaKnockoutJogoDto>> GerarFaseKnockoutAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (liga.Status != LigaStatus.PlayIn)
            throw new InvalidOperationException("Liga deve estar na fase PlayIn para gerar o bracket.");

        var jaExiste = await _db.LigaKnockoutJogos.AnyAsync(x => x.LigaId == ligaId, ct);
        if (jaExiste)
            throw new InvalidOperationException("Bracket já foi gerado.");

        if (liga.Tipo == TipoCompetition.Copa)
            return await GerarFaseKnockoutCopaAsync(ligaId, ct);

        // Pega classificação ordenada
        var classif = await _db.LigaClassificacoes
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .OrderBy(x => x.Posicao)
            .Include(x => x.Time)
            .ToListAsync(ct);

        if (classif.Count < 10)
            throw new InvalidOperationException("Precisa de ao menos 10 times classificados.");

        var pos = classif.Select(c => c.TimeId).ToArray();

        // Bracket fixo conforme /formato
        // PlayIn_A: 9º(idx8) vs 10º(idx9)
        // PlayIn_B: 7º(idx6) vs 8º(idx7)
        // PlayIn_C: TBD (preenchido após jogos A e B)
        // QF1: 1º(idx0) vs TBD (vencedor C)
        // QF2: 2º(idx1) vs TBD (vencedor B)
        // QF3: 3º(idx2) vs 6º(idx5)
        // QF4: 4º(idx3) vs 5º(idx4)
        // Semi1, Semi2, Final: TBD

        var jogos = new[]
        {
            new LigaKnockoutJogo { KnockoutJogoId = Guid.NewGuid(), LigaId = ligaId, Fase = FaseKnockout.PlayIn_A, TimeCasaId = pos[8], TimeForaId = pos[9] },
            new LigaKnockoutJogo { KnockoutJogoId = Guid.NewGuid(), LigaId = ligaId, Fase = FaseKnockout.PlayIn_B, TimeCasaId = pos[6], TimeForaId = pos[7] },
            new LigaKnockoutJogo { KnockoutJogoId = Guid.NewGuid(), LigaId = ligaId, Fase = FaseKnockout.PlayIn_C },
            new LigaKnockoutJogo { KnockoutJogoId = Guid.NewGuid(), LigaId = ligaId, Fase = FaseKnockout.QF1, TimeCasaId = pos[0] },
            new LigaKnockoutJogo { KnockoutJogoId = Guid.NewGuid(), LigaId = ligaId, Fase = FaseKnockout.QF2, TimeCasaId = pos[1] },
            new LigaKnockoutJogo { KnockoutJogoId = Guid.NewGuid(), LigaId = ligaId, Fase = FaseKnockout.QF3, TimeCasaId = pos[2], TimeForaId = pos[5] },
            new LigaKnockoutJogo { KnockoutJogoId = Guid.NewGuid(), LigaId = ligaId, Fase = FaseKnockout.QF4, TimeCasaId = pos[3], TimeForaId = pos[4] },
            new LigaKnockoutJogo { KnockoutJogoId = Guid.NewGuid(), LigaId = ligaId, Fase = FaseKnockout.Semi1 },
            new LigaKnockoutJogo { KnockoutJogoId = Guid.NewGuid(), LigaId = ligaId, Fase = FaseKnockout.Semi2 },
            new LigaKnockoutJogo { KnockoutJogoId = Guid.NewGuid(), LigaId = ligaId, Fase = FaseKnockout.Final }
        };

        _db.LigaKnockoutJogos.AddRange(jogos);
        await _db.SaveChangesAsync(ct);

        return await GetKnockoutDtosAsync(ligaId, ct);
    }

    public async Task<LigaPartidaDto> CriarPartidaKnockoutAsync(Guid knockoutJogoId, CancellationToken ct)
    {
        var jogo = await _db.LigaKnockoutJogos
            .FirstOrDefaultAsync(x => x.KnockoutJogoId == knockoutJogoId, ct)
            ?? throw new InvalidOperationException("Jogo não encontrado.");

        if (!jogo.TimeCasaId.HasValue || !jogo.TimeForaId.HasValue)
            throw new InvalidOperationException("Times ainda não definidos para este jogo.");

        if (jogo.PartidaId.HasValue)
            throw new InvalidOperationException("Partida já criada para este jogo.");

        // Knockout rodada: Numero = 0, shared for all knockout games of this liga
        var rodada = await _db.LigaRodadas.FirstOrDefaultAsync(x => x.LigaId == jogo.LigaId && x.Numero == 0, ct);
        if (rodada is null)
        {
            rodada = new LigaRodada { RodadaId = Guid.NewGuid(), LigaId = jogo.LigaId, Numero = 0 };
            _db.LigaRodadas.Add(rodada);
            await _db.SaveChangesAsync(ct);
        }

        var partida = new LigaPartida
        {
            PartidaId = Guid.NewGuid(),
            RodadaId = rodada.RodadaId,
            TimeCasaId = jogo.TimeCasaId.Value,
            TimeForaId = jogo.TimeForaId.Value,
            Status = PartidaStatus.EmAndamento,
            IniciadaEm = DateTime.UtcNow
        };

        _db.LigaPartidas.Add(partida);
        jogo.PartidaId = partida.PartidaId;
        await _db.SaveChangesAsync(ct);

        return await GetPartidaDtoAsync(partida.PartidaId, ct);
    }

    public async Task<LigaKnockoutJogoDto> EncerrarKnockoutJogoAsync(Guid knockoutJogoId, LigaEncerrarKnockoutRequest request, CancellationToken ct)
    {
        var jogo = await _db.LigaKnockoutJogos
            .Include(x => x.Partida)
            .FirstOrDefaultAsync(x => x.KnockoutJogoId == knockoutJogoId, ct)
            ?? throw new InvalidOperationException("Jogo knockout não encontrado.");

        if (jogo.VencedorId.HasValue)
            throw new InvalidOperationException("Jogo já encerrado.");

        if (!jogo.TimeCasaId.HasValue || !jogo.TimeForaId.HasValue)
            throw new InvalidOperationException("Times ainda não definidos para este jogo.");

        // Determina vencedor pelo placar da partida vinculada ou por penálties
        Guid vencedorId;
        if (jogo.Partida is not null)
        {
            var partida = jogo.Partida;
            if (request.TemPenaltis)
            {
                if (!request.PenaltisVencedorId.HasValue)
                    throw new InvalidOperationException("Informe o vencedor dos penálties.");

                vencedorId = request.PenaltisVencedorId.Value;
                partida.TemPenaltis = true;
                partida.PenaltisVencedorId = vencedorId;
            }
            else if (partida.GolsCasa == partida.GolsFora)
            {
                throw new InvalidOperationException(
                    "O jogo terminou empatado. Marque \"Foi para pênaltis?\" e informe o vencedor.");
            }
            else
            {
                vencedorId = partida.GolsCasa > partida.GolsFora
                    ? jogo.TimeCasaId!.Value
                    : jogo.TimeForaId!.Value;
            }
        }
        else
        {
            throw new InvalidOperationException("Crie e inicie a partida deste jogo antes de encerrá-lo.");
        }

        jogo.VencedorId = vencedorId;

        // Encerra a partida vinculada (evita ficar EmAndamento para sempre).
        if (jogo.Partida is not null && jogo.Partida.Status != PartidaStatus.Encerrada)
        {
            jogo.Partida.Status = PartidaStatus.Encerrada;
            jogo.Partida.EncerradaEm = _time.GetUtcNow().UtcDateTime;
            await RegistrarEscalacoesAsync(jogo.Partida, ct);
        }

        await _db.SaveChangesAsync(ct);

        // Avança vencedor (e perdedor para PlayIn_C) no bracket
        await AvancarBracketAsync(jogo, vencedorId, ct);

        return await GetKnockoutJogoDtoAsync(knockoutJogoId, ct);
    }

    // ── Copa grupos ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<LigaGrupoTimeDto>> ListGruposAsync(Guid ligaId, CancellationToken ct)
    {
        var grupos = await _db.LigaGruposTimes
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .Include(x => x.Time)
            .OrderBy(x => x.Grupo)
            .ThenBy(x => x.Time.TeamName)
            .ToListAsync(ct);

        return grupos.Select(g => new LigaGrupoTimeDto(g.LigaId, g.TimeId, g.Time.TeamName, g.Grupo)).ToArray();
    }

    /// <summary>Grupos da Copa no formato atual (A, B, C e D).</summary>
    private const int GruposDaCopa = 4;

    /// <summary>
    /// Tamanho de cada grupo, o mais parecido possível: 16 times viram 4+4+4+4 e 18 viram 4+4+5+5,
    /// com os grupos maiores no fim (C e D).
    /// </summary>
    private static List<int> TamanhosDosGrupos(int totalTimes)
    {
        if (totalTimes < 8) return new List<int>();

        var grupos = Math.Min(GruposDaCopa, totalTimes / 3);
        var baseTimes = totalTimes / grupos;
        var sobra = totalTimes % grupos;

        return Enumerable.Range(0, grupos)
            .Select(i => baseTimes + (i >= grupos - sobra ? 1 : 0))
            .ToList();
    }

    public async Task<LigaCopaSorteioDto?> GetSorteioCopaAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(x => x.LigaId == ligaId, ct);
        if (liga is null || liga.Tipo != TipoCompetition.Copa) return null;

        var potes = await _db.LigaCopaPotes.AsNoTracking()
            .Where(p => p.LigaId == ligaId)
            .Select(p => new { p.TimeId, p.Time.TeamName, p.Pote })
            .ToListAsync(ct);

        var grupos = await _db.LigaGruposTimes.AsNoTracking()
            .Where(g => g.LigaId == ligaId)
            .ToDictionaryAsync(g => g.TimeId, g => g.Grupo, ct);

        var times = potes
            .Select(p => new LigaCopaPoteTimeDto(p.TimeId, p.TeamName, p.Pote, grupos.TryGetValue(p.TimeId, out var g) ? g : null))
            .OrderBy(t => t.Pote).ThenBy(t => t.TimeNome)
            .ToList();

        var tamanhos = TamanhosDosGrupos(times.Count);
        var impedimento =
            liga.Status != LigaStatus.Criada ? "A Copa já começou: o sorteio só vale antes de iniciar."
            : times.Count == 0 ? "Monte os potes antes de sortear."
            : times.Count < 8 ? $"São {times.Count} times: a Copa precisa de pelo menos 8 (2 grupos)."
            : null;

        return new LigaCopaSorteioDto(
            ligaId,
            tamanhos,
            grupos.Count > 0,
            impedimento is null,
            impedimento,
            times);
    }

    public async Task<LigaCopaSorteioDto> ConfigurarPotesCopaAsync(Guid ligaId, LigaCopaPotesRequest request, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (liga.Tipo != TipoCompetition.Copa)
            throw new InvalidOperationException("Os potes são exclusivos da Copa.");

        if (liga.Status != LigaStatus.Criada)
            throw new InvalidOperationException("Os potes só podem ser mexidos antes de iniciar a Copa.");

        var escolhidos = request.PotePorTime.Where(x => x.Value > 0).ToList();
        if (escolhidos.Any(x => x.Value > 9))
            throw new InvalidOperationException("Número de pote inválido.");

        var anteriores = await _db.LigaCopaPotes.Where(p => p.LigaId == ligaId).ToListAsync(ct);
        _db.LigaCopaPotes.RemoveRange(anteriores);

        foreach (var (timeId, pote) in escolhidos)
            _db.LigaCopaPotes.Add(new LigaCopaPote { Id = Guid.NewGuid(), LigaId = ligaId, TimeId = timeId, Pote = pote });

        await _db.SaveChangesAsync(ct);
        return (await GetSorteioCopaAsync(ligaId, ct))!;
    }

    public async Task<IReadOnlyList<LigaGrupoTimeDto>> SortearCopaAsync(Guid ligaId, CancellationToken ct)
    {
        var sorteio = await GetSorteioCopaAsync(ligaId, ct)
            ?? throw new InvalidOperationException("Copa não encontrada.");

        if (!sorteio.PodeSortear)
            throw new InvalidOperationException(sorteio.Impedimento ?? "Não é possível sortear agora.");

        var grupos = Enumerable.Range(0, sorteio.TotalGrupos).Select(i => (GrupoCopa)i).ToList();
        var vagasPorGrupo = grupos.ToDictionary(g => g, _ => new List<Guid>());
        var vagasRestantes = grupos
            .Select((g, i) => (Grupo: g, Vagas: sorteio.TamanhosDosGrupos[i]))
            .ToDictionary(x => x.Grupo, x => x.Vagas);
        var random = Random.Shared;

        // Pote a pote: cada grupo recebe um time do pote antes de qualquer grupo receber o segundo.
        // Entre os empatados, vai para quem tem mais vagas sobrando — assim o resto do pote cai
        // nos grupos maiores, e o desempate final é no sorteio.
        foreach (var pote in sorteio.Times.GroupBy(t => t.Pote).OrderBy(p => p.Key))
        {
            var recebidosDoPote = grupos.ToDictionary(g => g, _ => 0);

            foreach (var timeId in pote.Select(t => t.TimeId).OrderBy(_ => random.Next()))
            {
                var destino = vagasRestantes
                    .Where(v => v.Value > 0)
                    .OrderBy(v => recebidosDoPote[v.Key])
                    .ThenByDescending(v => v.Value)
                    .ThenBy(_ => random.Next())
                    .First().Key;

                vagasPorGrupo[destino].Add(timeId);
                vagasRestantes[destino]--;
                recebidosDoPote[destino]++;
            }
        }

        var antigos = await _db.LigaGruposTimes.Where(g => g.LigaId == ligaId).ToListAsync(ct);
        _db.LigaGruposTimes.RemoveRange(antigos);

        foreach (var (grupo, times) in vagasPorGrupo)
            foreach (var timeId in times)
                _db.LigaGruposTimes.Add(new LigaGrupoTime { Id = Guid.NewGuid(), LigaId = ligaId, TimeId = timeId, Grupo = grupo });

        var liga = await _db.Ligas.FirstAsync(x => x.LigaId == ligaId, ct);
        var maior = sorteio.TamanhosDosGrupos.Max();
        liga.TotalRodadas = maior % 2 == 0 ? maior - 1 : maior; // round-robin do maior grupo (ímpar tem folga)
        liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
        return await ListGruposAsync(ligaId, ct);
    }

    public async Task ConfigurarGruposCopaAsync(Guid ligaId, LigaConfigurarGruposRequest request, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (liga.Tipo != TipoCompetition.Copa)
            throw new InvalidOperationException("Esta liga não é uma Copa.");

        if (liga.Status != LigaStatus.Criada)
            throw new InvalidOperationException("Grupos só podem ser configurados antes de iniciar a copa.");

        var porGrupo = request.GrupoPorTime
            .GroupBy(x => x.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList());

        // O mata-mata só existe com 2 grupos (semis) ou 4 (quartas).
        if (porGrupo.Count is not (2 or 4))
            throw new InvalidOperationException("A Copa precisa de 2 grupos (A e B) ou 4 (A, B, C e D).");

        if (porGrupo.Count == 2 && !(porGrupo.ContainsKey(GrupoCopa.A) && porGrupo.ContainsKey(GrupoCopa.B)))
            throw new InvalidOperationException("Com 2 grupos, use os grupos A e B.");

        if (porGrupo.Values.Any(t => t.Count < 2))
            throw new InvalidOperationException("Cada grupo precisa de pelo menos 2 times (os 2 primeiros vão ao mata-mata).");

        // Remove grupos anteriores
        var existentes = await _db.LigaGruposTimes.Where(x => x.LigaId == ligaId).ToListAsync(ct);
        _db.LigaGruposTimes.RemoveRange(existentes);

        foreach (var (timeId, grupo) in request.GrupoPorTime)
            _db.LigaGruposTimes.Add(new LigaGrupoTime { Id = Guid.NewGuid(), LigaId = ligaId, TimeId = timeId, Grupo = grupo });

        // Round-robin do maior grupo, igual ao sorteio (grupo ímpar tem folga).
        var maior = porGrupo.Values.Max(t => t.Count);
        liga.TotalRodadas = maior % 2 == 0 ? maior - 1 : maior;
        liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ListTimesLigaAsync(Guid ligaId, CancellationToken ct)
        => await _db.LigaTimes.AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .Select(x => x.TimeId)
            .ToListAsync(ct);

    public async Task<LigaDto> DefinirConfrontoFinalAsync(Guid ligaId, Guid? timeAId, Guid? timeBId, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (timeAId is null || timeBId is null)
        {
            liga.ConfrontoFinalTimeAId = null;
            liga.ConfrontoFinalTimeBId = null;
        }
        else
        {
            if (timeAId == timeBId)
                throw new InvalidOperationException("Escolha dois times diferentes.");

            var inscritos = await _db.LigaTimes
                .Where(x => x.LigaId == ligaId && (x.TimeId == timeAId || x.TimeId == timeBId))
                .CountAsync(ct);

            if (inscritos < 2)
                throw new InvalidOperationException("Os dois times precisam estar inscritos nesta liga.");

            liga.ConfrontoFinalTimeAId = timeAId;
            liga.ConfrontoFinalTimeBId = timeBId;
        }

        liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        return ToDto(liga);
    }

    public async Task ConfigurarTimesLigaAsync(Guid ligaId, IReadOnlyList<Guid> teamIds, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        // Copa inscreve pelos grupos; Liga e Supercopa inscrevem a lista de times direto.
        if (liga.Tipo == TipoCompetition.Copa)
            throw new InvalidOperationException("Na Copa a inscrição é feita pelos grupos, não pela lista de times.");

        if (liga.Status != LigaStatus.Criada)
            throw new InvalidOperationException("Os times só podem ser configurados antes de iniciar a liga.");

        var distinct = teamIds.Distinct().ToList();
        if (distinct.Count < 2)
            throw new InvalidOperationException("Selecione ao menos 2 times.");

        // Um time disputa só uma divisão por temporada.
        if (liga.Temporada is not null && liga.Divisao is not null)
        {
            var emOutraDivisao = await _db.LigaTimes
                .Where(x => distinct.Contains(x.TimeId)
                            && x.LigaId != ligaId
                            && x.Liga.Temporada == liga.Temporada
                            && x.Liga.Divisao != null
                            && x.Liga.Divisao != liga.Divisao)
                .Select(x => new { x.Time.TeamName, x.Liga.Divisao })
                .FirstOrDefaultAsync(ct);

            if (emOutraDivisao is not null)
                throw new InvalidOperationException(
                    $"{emOutraDivisao.TeamName} já está na {LigaLabels.DivisaoNome(emOutraDivisao.Divisao!.Value)} da temporada {liga.Temporada}.");
        }

        var existentes = await _db.LigaTimes.Where(x => x.LigaId == ligaId).ToListAsync(ct);
        _db.LigaTimes.RemoveRange(existentes);

        foreach (var timeId in distinct)
            _db.LigaTimes.Add(new LigaTime { Id = Guid.NewGuid(), LigaId = ligaId, TimeId = timeId });

        // Total de rodadas = nº de times - 1 (round-robin simples, cada um enfrenta o outro uma vez).
        liga.TotalRodadas = distinct.Count - 1;
        liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<LigaKnockoutJogoDto>> GerarFaseKnockoutCopaAsync(Guid ligaId, CancellationToken ct)
    {
        // Top 2 de cada grupo pelos critérios da Copa, já com o vencedor do jogo decisivo à frente.
        var classifs = await _db.LigaClassificacoes
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .ToListAsync(ct);

        var decisivos = await CarregarJogosDecisivosAsync(ligaId, ct);

        var gruposDaCopa = classifs
            .Where(c => c.Grupo is not null)
            .Select(c => c.Grupo!.Value)
            .Distinct()
            .OrderBy(g => g)
            .ToList();

        var classificados = gruposDaCopa.ToDictionary(
            g => g,
            g => OrdenarGrupoCopa(classifs, g, decisivos).Take(2).ToList());

        if (classificados.Values.Any(c => c.Count < 2))
            throw new InvalidOperationException("Precisa de ao menos 2 classificados por grupo.");

        List<LigaKnockoutJogo> jogos;

        if (gruposDaCopa.Count >= 4)
        {
            // Quartas cruzando os grupos: 1ºA x 2ºB, 1ºB x 2ºA, 1ºC x 2ºD, 1ºD x 2ºC.
            var (a, b, c, d) = (gruposDaCopa[0], gruposDaCopa[1], gruposDaCopa[2], gruposDaCopa[3]);
            jogos = new List<LigaKnockoutJogo>
            {
                Jogo(FaseKnockout.QF1, classificados[a][0].TimeId, classificados[b][1].TimeId),
                Jogo(FaseKnockout.QF2, classificados[b][0].TimeId, classificados[a][1].TimeId),
                Jogo(FaseKnockout.QF3, classificados[c][0].TimeId, classificados[d][1].TimeId),
                Jogo(FaseKnockout.QF4, classificados[d][0].TimeId, classificados[c][1].TimeId),
                Jogo(FaseKnockout.Semi1),
                Jogo(FaseKnockout.Semi2),
                Jogo(FaseKnockout.Final)
            };
        }
        else
        {
            // Dois grupos: semifinais dentro do próprio grupo (1º x 2º).
            jogos = new List<LigaKnockoutJogo>
            {
                Jogo(FaseKnockout.Semi1, classificados[gruposDaCopa[0]][0].TimeId, classificados[gruposDaCopa[0]][1].TimeId),
                Jogo(FaseKnockout.Semi2, classificados[gruposDaCopa[1]][0].TimeId, classificados[gruposDaCopa[1]][1].TimeId),
                Jogo(FaseKnockout.Final)
            };
        }

        _db.LigaKnockoutJogos.AddRange(jogos);

        LigaKnockoutJogo Jogo(FaseKnockout fase, Guid? casa = null, Guid? fora = null) =>
            new() { KnockoutJogoId = Guid.NewGuid(), LigaId = ligaId, Fase = fase, TimeCasaId = casa, TimeForaId = fora };
        await _db.SaveChangesAsync(ct);

        return await GetKnockoutDtosAsync(ligaId, ct);
    }

    // ── Jogo decisivo da Copa ──────────────────────────────────

    private static DesempateStats StatsDaClassificacao(LigaClassificacao c) =>
        new(c.Pontos, c.Vitorias, c.SaldoGols, c.GolsPro);

    /// <summary>Resultados dos jogos decisivos já encerrados desta competição.</summary>
    private async Task<JogosDecisivos> CarregarJogosDecisivosAsync(Guid ligaId, CancellationToken ct)
    {
        var jogos = await _db.LigaPartidas
            .AsNoTracking()
            .Where(p => p.Rodada.LigaId == ligaId && p.Rodada.Desempate && p.Status == PartidaStatus.Encerrada)
            .Select(p => new { p.TimeCasaId, p.TimeForaId, p.GolsCasa, p.GolsFora, p.TemPenaltis, p.PenaltisVencedorId })
            .ToListAsync(ct);

        var resultados = new List<(Guid, Guid)>();
        foreach (var j in jogos)
        {
            var vencedor = LigaDesempate.VencedorDoJogoDecisivo(
                j.TimeCasaId, j.TimeForaId, j.GolsCasa, j.GolsFora, j.TemPenaltis, j.PenaltisVencedorId);

            if (vencedor is Guid v)
                resultados.Add((v, v == j.TimeCasaId ? j.TimeForaId : j.TimeCasaId));
        }

        return new JogosDecisivos(resultados);
    }

    /// <summary>Grupo da Copa já ordenado pelos critérios e pelos jogos decisivos encerrados.</summary>
    private static List<LigaClassificacao> OrdenarGrupoCopa(
        IEnumerable<LigaClassificacao> classifs, GrupoCopa grupo, JogosDecisivos decisivos) =>
        LigaDesempate.Ordenar(
            classifs.Where(c => c.Grupo == grupo),
            TipoCompetition.Copa,
            c => c.TimeId,
            StatsDaClassificacao,
            Array.Empty<ConfrontoDireto>(),
            decisivos);

    public async Task<IReadOnlyList<LigaEmpateCopaDto>> ListEmpatesCopaAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(x => x.LigaId == ligaId, ct);
        if (liga is null || liga.Tipo != TipoCompetition.Copa)
            return Array.Empty<LigaEmpateCopaDto>();

        var classifs = await _db.LigaClassificacoes
            .AsNoTracking()
            .Include(x => x.Time)
            .Where(x => x.LigaId == ligaId)
            .ToListAsync(ct);

        var partidas = await _db.LigaPartidas
            .AsNoTracking()
            .Where(p => p.Rodada.LigaId == ligaId && p.Rodada.Desempate)
            .ToListAsync(ct);

        var decisivos = await CarregarJogosDecisivosAsync(ligaId, ct);
        var empates = new List<LigaEmpateCopaDto>();

        foreach (var grupo in classifs.Where(c => c.Grupo is not null).Select(c => c.Grupo!.Value).Distinct().OrderBy(g => g))
        {
            var doGrupo = OrdenarGrupoCopa(classifs, grupo, decisivos);
            var posicoes = LigaDesempate.PosicoesCopa(doGrupo, c => c.TimeId, StatsDaClassificacao, decisivos);

            for (int i = 0; i + 1 < doGrupo.Count; i++)
            {
                var a = doGrupo[i];
                var b = doGrupo[i + 1];

                var partida = partidas.FirstOrDefault(p =>
                    (p.TimeCasaId == a.TimeId && p.TimeForaId == b.TimeId) ||
                    (p.TimeCasaId == b.TimeId && p.TimeForaId == a.TimeId));

                // Interessa o empate ainda em aberto e também o que já virou jogo decisivo.
                if (posicoes[i] != posicoes[i + 1] && partida is null) continue;

                var vencedorId = partida is null ? null : decisivos.VencedorEntre(a.TimeId, b.TimeId);

                // O placar sai na ordem em que a dupla é exibida, e não na de casa/fora da partida.
                var aEmCasa = partida?.TimeCasaId == a.TimeId;
                var golsA = partida is null ? null : (int?)(aEmCasa ? partida.GolsCasa : partida.GolsFora);
                var golsB = partida is null ? null : (int?)(aEmCasa ? partida.GolsFora : partida.GolsCasa);

                empates.Add(new LigaEmpateCopaDto(
                    grupo,
                    posicoes[i],
                    a.TimeId, a.Time.TeamName,
                    b.TimeId, b.Time.TeamName,
                    partida?.PartidaId,
                    partida?.Status,
                    golsA,
                    golsB,
                    vencedorId,
                    vencedorId is null ? null : classifs.First(c => c.TimeId == vencedorId).Time.TeamName));
            }
        }

        return empates;
    }

    public async Task<LigaPartidaDto> GerarJogoDecisivoCopaAsync(
        Guid ligaId, Guid timeAId, Guid timeBId, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (liga.Tipo != TipoCompetition.Copa)
            throw new InvalidOperationException("O jogo decisivo de grupo aplica-se apenas à Copa.");

        if (liga.Status != LigaStatus.PrimeiraFase)
            throw new InvalidOperationException("O jogo decisivo só pode ser criado durante a fase de grupos.");

        var classifs = await _db.LigaClassificacoes
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .ToListAsync(ct);

        var a = classifs.FirstOrDefault(x => x.TimeId == timeAId)
            ?? throw new InvalidOperationException("Time não encontrado na classificação desta copa.");
        var b = classifs.FirstOrDefault(x => x.TimeId == timeBId)
            ?? throw new InvalidOperationException("Time não encontrado na classificação desta copa.");

        if (a.Grupo is null || a.Grupo != b.Grupo)
            throw new InvalidOperationException("Os dois times precisam ser do mesmo grupo.");

        if (!LigaDesempate.EmpatadosNaCopa(StatsDaClassificacao(a), StatsDaClassificacao(b)))
            throw new InvalidOperationException("Os times não estão empatados em Pontos, Vitórias e Saldo de Gols.");

        var partidaId = await AdicionarJogoDecisivoAsync(liga, timeAId, timeBId, ct);
        return await GetPartidaDtoAsync(partidaId, ct);
    }

    /// <summary>
    /// Cria o jogo decisivo na rodada de desempate (depois da última regular), que não soma pontos
    /// e, encerrado, coloca o vencedor à frente do adversário.
    /// </summary>
    private async Task<Guid> AdicionarJogoDecisivoAsync(Liga liga, Guid timeAId, Guid timeBId, CancellationToken ct)
    {
        var rodada = await _db.LigaRodadas
            .Include(r => r.Partidas)
            .FirstOrDefaultAsync(x => x.LigaId == liga.LigaId && x.Desempate, ct);

        if (rodada is null)
        {
            // Entra como a rodada seguinte à última (Copa: rodada 7).
            var maiorNumero = await _db.LigaRodadas
                .Where(x => x.LigaId == liga.LigaId)
                .MaxAsync(x => (int?)x.Numero, ct) ?? 0;

            rodada = new LigaRodada
            {
                RodadaId = Guid.NewGuid(),
                LigaId = liga.LigaId,
                Numero = Math.Max(liga.TotalRodadas, maiorNumero) + 1,
                Desempate = true
            };
            _db.LigaRodadas.Add(rodada);
        }
        else if (rodada.Partidas.Any(p =>
                     (p.TimeCasaId == timeAId && p.TimeForaId == timeBId) ||
                     (p.TimeCasaId == timeBId && p.TimeForaId == timeAId)))
        {
            throw new InvalidOperationException("O jogo decisivo entre esses times já existe.");
        }

        var partida = new LigaPartida
        {
            PartidaId = Guid.NewGuid(),
            RodadaId = rodada.RodadaId,
            TimeCasaId = timeAId,
            TimeForaId = timeBId,
            Status = PartidaStatus.Agendada
        };
        rodada.Partidas.Add(partida);

        await _db.SaveChangesAsync(ct);
        return partida.PartidaId;
    }

    // ── Desempate nas zonas (Liga com acesso/rebaixamento) ───────────────────

    private sealed record EmpateZona(
        int Posicao,
        ZonaClassificacao ZonaA,
        ZonaClassificacao ZonaB,
        LigaClassificacao TimeA,
        LigaClassificacao TimeB,
        LigaPartida? Partida,
        Guid? VencedorId);

    /// <summary>
    /// Empates que decidem zona: iguais em Pontos, Vitórias, Saldo, Gols Pró e confronto direto,
    /// numa fronteira de zona, depois de todos os jogos regulares. Inclui os já levados a jogo decisivo.
    /// </summary>
    private async Task<List<EmpateZona>> CalcularEmpatesZonaAsync(Liga liga, CancellationToken ct)
    {
        var regra = LigaRegraZonas.De(liga.Tipo, liga.Divisao, liga.VagasDiretas, liga.VagasPlayoff);
        if (!regra.TemAcessoRebaixamento) return new List<EmpateZona>();

        var partidasRegulares = _db.LigaPartidas
            .Where(p => p.Rodada.LigaId == liga.LigaId && p.Rodada.Numero > 0 && !p.Rodada.Desempate);
        if (!await partidasRegulares.AnyAsync(ct)
            || await partidasRegulares.AnyAsync(p => p.Status != PartidaStatus.Encerrada, ct))
            return new List<EmpateZona>();

        var classifs = await _db.LigaClassificacoes
            .AsNoTracking()
            .Include(x => x.Time)
            .Where(x => x.LigaId == liga.LigaId)
            .ToListAsync(ct);

        var confrontos = await CarregarConfrontosAsync(liga.LigaId, ct);
        var decisivos = await CarregarJogosDecisivosAsync(liga.LigaId, ct);
        var jogosDecisivos = await _db.LigaPartidas
            .AsNoTracking()
            .Where(p => p.Rodada.LigaId == liga.LigaId && p.Rodada.Desempate)
            .ToListAsync(ct);

        var ordenados = LigaDesempate.Ordenar(classifs, liga.Tipo, c => c.TimeId, StatsDaClassificacao, confrontos, decisivos);
        var semDecisivo = LigaDesempate.PosicoesLiga(ordenados, c => c.TimeId, StatsDaClassificacao, confrontos);
        var total = ordenados.Count;
        var empates = new List<EmpateZona>();

        foreach (var posicao in LigaZonas.Fronteiras(regra, total))
        {
            var a = ordenados[posicao - 1];
            var b = ordenados[posicao];

            // Empate total ignorando o jogo decisivo: assim o empate continua listado depois de resolvido.
            if (semDecisivo[posicao - 1] != semDecisivo[posicao]) continue;

            var partida = jogosDecisivos.FirstOrDefault(p =>
                (p.TimeCasaId == a.TimeId && p.TimeForaId == b.TimeId) ||
                (p.TimeCasaId == b.TimeId && p.TimeForaId == a.TimeId));

            empates.Add(new EmpateZona(
                posicao,
                LigaZonas.Zona(regra, posicao, total),
                LigaZonas.Zona(regra, posicao + 1, total),
                a, b, partida,
                partida is null ? null : decisivos.VencedorEntre(a.TimeId, b.TimeId)));
        }

        return empates;
    }

    public async Task<IReadOnlyList<LigaEmpateZonaDto>> ListEmpatesZonaAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(x => x.LigaId == ligaId, ct);
        if (liga is null) return Array.Empty<LigaEmpateZonaDto>();

        var empates = await CalcularEmpatesZonaAsync(liga, ct);

        return empates.Select(e =>
        {
            // Placar na ordem da dupla exibida, não na de casa/fora.
            var aEmCasa = e.Partida?.TimeCasaId == e.TimeA.TimeId;
            return new LigaEmpateZonaDto(
                e.Posicao,
                RotuloZona(e.ZonaA),
                RotuloZona(e.ZonaB),
                e.TimeA.TimeId, e.TimeA.Time.TeamName,
                e.TimeB.TimeId, e.TimeB.Time.TeamName,
                e.Partida?.PartidaId,
                e.Partida?.Status,
                e.Partida is null ? null : aEmCasa ? e.Partida.GolsCasa : e.Partida.GolsFora,
                e.Partida is null ? null : aEmCasa ? e.Partida.GolsFora : e.Partida.GolsCasa,
                e.VencedorId,
                e.VencedorId == e.TimeA.TimeId ? e.TimeA.Time.TeamName
                    : e.VencedorId == e.TimeB.TimeId ? e.TimeB.Time.TeamName : null);
        }).ToList();

        static string RotuloZona(ZonaClassificacao z) => z == ZonaClassificacao.Nenhuma ? "Fora das zonas" : LigaZonas.Rotulo(z);
    }

    public async Task<LigaPartidaDto> GerarJogoDecisivoZonaAsync(Guid ligaId, Guid timeAId, Guid timeBId, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (liga.Status != LigaStatus.PrimeiraFase)
            throw new InvalidOperationException("O jogo decisivo de zona só pode ser criado antes de encerrar a temporada.");

        var empate = (await CalcularEmpatesZonaAsync(liga, ct)).FirstOrDefault(e =>
            (e.TimeA.TimeId == timeAId && e.TimeB.TimeId == timeBId) ||
            (e.TimeA.TimeId == timeBId && e.TimeB.TimeId == timeAId))
            ?? throw new InvalidOperationException(
                "Esses times não estão empatados numa posição de zona (ou ainda há jogos regulares a disputar).");

        var partidaId = await AdicionarJogoDecisivoAsync(liga, empate.TimeA.TimeId, empate.TimeB.TimeId, ct);
        return await GetPartidaDtoAsync(partidaId, ct);
    }

    /// <summary>Impede encerrar a temporada com empate de zona sem jogo decisivo resolvido.</summary>
    private async Task GarantirDesempatesDeZonaAsync(Liga liga, CancellationToken ct)
    {
        var pendente = (await CalcularEmpatesZonaAsync(liga, ct)).FirstOrDefault(e => e.VencedorId is null);
        if (pendente is null) return;

        throw new InvalidOperationException(
            $"{pendente.TimeA.Time.TeamName} e {pendente.TimeB.Time.TeamName} estão empatados em todos os critérios " +
            $"(inclusive confronto direto), e a diferença é entre \"{LigaZonas.Rotulo(pendente.ZonaA)}\" e " +
            $"\"{(pendente.ZonaB == ZonaClassificacao.Nenhuma ? "Fora das zonas" : LigaZonas.Rotulo(pendente.ZonaB))}\". " +
            "Crie e encerre o jogo decisivo (aba Jogo Decisivo) antes de encerrar a temporada.");
    }

    private async Task<List<ConfrontoDireto>> CarregarConfrontosAsync(Guid ligaId, CancellationToken ct)
    {
        var encerradas = await _db.LigaPartidas
            .AsNoTracking()
            .Where(p => p.Rodada.LigaId == ligaId && !p.Rodada.Desempate
                        && p.Status == PartidaStatus.Encerrada && !p.IsWO)
            .Select(p => new { p.TimeCasaId, p.TimeForaId, p.GolsCasa, p.GolsFora })
            .ToListAsync(ct);

        return encerradas
            .Select(p => new ConfrontoDireto(p.TimeCasaId, p.TimeForaId, p.GolsCasa, p.GolsFora))
            .ToList();
    }

    /// <summary>
    /// Impede encerrar a fase de grupos com empate sem solução nas 3 primeiras posições:
    /// a vaga (1º-2º) e o cruzamento das semis dependem dessa ordem.
    /// </summary>
    private async Task GarantirDesempatesDaCopaAsync(Guid ligaId, CancellationToken ct)
    {
        var classifs = await _db.LigaClassificacoes
            .AsNoTracking()
            .Include(x => x.Time)
            .Where(x => x.LigaId == ligaId)
            .ToListAsync(ct);

        var decisivos = await CarregarJogosDecisivosAsync(ligaId, ct);

        // Empate dentro da faixa que decide a vaga (1º-2º) deixaria o mata-mata indefinido.
        foreach (var grupo in classifs.Where(c => c.Grupo is not null).Select(c => c.Grupo!.Value).Distinct().OrderBy(g => g))
        {
            var doGrupo = OrdenarGrupoCopa(classifs, grupo, decisivos);
            var posicoes = LigaDesempate.PosicoesCopa(doGrupo, c => c.TimeId, StatsDaClassificacao, decisivos);

            for (int i = 0; i + 1 < doGrupo.Count && posicoes[i] <= 2; i++)
            {
                if (posicoes[i] != posicoes[i + 1]) continue;

                throw new InvalidOperationException(
                    $"Grupo {grupo}: {doGrupo[i].Time.TeamName} e {doGrupo[i + 1].Time.TeamName} estão empatados em " +
                    "Pontos, Vitórias e Saldo de Gols. Crie e encerre o jogo decisivo antes de encerrar a fase de grupos.");
            }
        }
    }

    // ── Tiebreaker (Liga) ─────────────────────────────────────────────────────

    // Numero sentinela para os jogos de desempate (excluídos da classificação regular, que usa Numero > 0)
    private const int NumeroMiniLiga = 0;
    private const int NumeroJogoDecisivo = -1;

    public async Task<LigaDto> IniciarDecisaoCampeaoAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (liga.Status != LigaStatus.DecisaoCampeao)
            throw new InvalidOperationException("Liga não está em Decisão de Campeão.");

        // Pega os 2 times empatados no topo
        var classif = await _db.LigaClassificacoes
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .OrderBy(x => x.Posicao)
            .Take(2)
            .ToListAsync(ct);

        if (classif.Count < 2)
            throw new InvalidOperationException("Times não encontrados para decisão.");

        await CriarJogoDecisivoAsync(ligaId, classif[0].TimeId, classif[1].TimeId, ct);

        liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);
        return ToDto(liga);
    }

    /// <summary>Cria o jogo único de desempate (Numero = -1) entre dois times. Idempotente.</summary>
    private async Task CriarJogoDecisivoAsync(Guid ligaId, Guid timeCasaId, Guid timeForaId, CancellationToken ct)
    {
        var jaExiste = await _db.LigaRodadas.AnyAsync(x => x.LigaId == ligaId && x.Numero == NumeroJogoDecisivo, ct);
        if (jaExiste) return;

        var rodada = new LigaRodada
        {
            RodadaId = Guid.NewGuid(),
            LigaId = ligaId,
            Numero = NumeroJogoDecisivo
        };
        rodada.Partidas.Add(new LigaPartida
        {
            PartidaId = Guid.NewGuid(),
            TimeCasaId = timeCasaId,
            TimeForaId = timeForaId,
            Status = PartidaStatus.Agendada
        });
        _db.LigaRodadas.Add(rodada);
    }

    public async Task<LigaDto> ConcluirDecisaoCampeaoAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (liga.Status != LigaStatus.DecisaoCampeao)
            throw new InvalidOperationException("Liga não está em Decisão de Campeão.");

        var partida = await _db.LigaPartidas
            .AsNoTracking()
            .Where(x => x.Rodada.LigaId == ligaId && x.Rodada.Numero == NumeroJogoDecisivo)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Jogo decisivo ainda não foi criado. Use \"Criar Jogo Decisivo\" primeiro.");

        if (partida.Status != PartidaStatus.Encerrada)
            throw new InvalidOperationException("O jogo decisivo ainda não foi encerrado.");

        Guid campeaoId;
        if (partida.GolsCasa != partida.GolsFora)
        {
            campeaoId = partida.GolsCasa > partida.GolsFora ? partida.TimeCasaId : partida.TimeForaId;
        }
        else if (partida.TemPenaltis && partida.PenaltisVencedorId is Guid pen)
        {
            campeaoId = pen;
        }
        else
        {
            throw new InvalidOperationException(
                "O jogo decisivo terminou empatado. Registre o vencedor nos pênaltis (W.O. ou pênaltis) antes de concluir.");
        }

        liga.Status = LigaStatus.Encerrada;
        liga.CampeaoTimeId = campeaoId;
        liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);
        await RecalcularPosicoesAsync(ligaId, ct);
        return ToDto(liga);
    }

    public async Task<LigaDto> IniciarMiniLigaAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (liga.Status != LigaStatus.MiniLiga)
            throw new InvalidOperationException("Liga não está em MiniLiga.");

        // Busca os times empatados no topo
        var classif = await _db.LigaClassificacoes
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .OrderBy(x => x.Posicao)
            .ToListAsync(ct);

        var lider = classif.FirstOrDefault() ?? throw new InvalidOperationException("Classificação vazia.");
        // Mesmo critério do encerramento: empate em pontos com o líder.
        var empatados = classif
            .Where(c => c.Pontos == lider.Pontos)
            .Select(c => c.TimeId)
            .ToList();

        if (empatados.Count < 3)
            throw new InvalidOperationException("Mini liga requer 3 ou mais times empatados.");

        // Gera rodadas de round-robin entre os empatados (Numero = 0 exclui da classificação principal)
        var rodadasExistentes = await _db.LigaRodadas.CountAsync(x => x.LigaId == ligaId && x.Numero == NumeroMiniLiga, ct);
        if (rodadasExistentes == 0)
        {
            var jogos = GerarRoundRobinParcial(empatados, empatados.Count - 1);
            foreach (var (r, rodadaJogos) in jogos.Select((j, i) => (i, j)))
            {
                var rodada = new LigaRodada
                {
                    RodadaId = Guid.NewGuid(),
                    LigaId = ligaId,
                    Numero = NumeroMiniLiga
                };
                foreach (var (casa, fora) in rodadaJogos)
                    rodada.Partidas.Add(new LigaPartida { PartidaId = Guid.NewGuid(), TimeCasaId = casa, TimeForaId = fora, Status = PartidaStatus.Agendada });
                _db.LigaRodadas.Add(rodada);
            }
        }

        liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);
        return ToDto(liga);
    }

    public async Task<LigaDto> ConcluirMiniLigaAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (liga.Status != LigaStatus.MiniLiga)
            throw new InvalidOperationException("Liga não está em Mini Liga.");

        // Times empatados que disputam a mini liga
        var classif = await _db.LigaClassificacoes
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .OrderBy(x => x.Posicao)
            .ToListAsync(ct);

        var lider = classif.FirstOrDefault() ?? throw new InvalidOperationException("Classificação vazia.");
        // Mesmo critério do encerramento: empate em pontos com o líder.
        var empatados = classif
            .Where(c => c.Pontos == lider.Pontos)
            .Select(c => c.TimeId)
            .ToHashSet();

        // Jogos da mini liga (Numero = 0)
        var jogos = await _db.LigaPartidas
            .AsNoTracking()
            .Where(x => x.Rodada.LigaId == ligaId && x.Rodada.Numero == NumeroMiniLiga)
            .ToListAsync(ct);

        if (jogos.Count == 0)
            throw new InvalidOperationException("A mini liga ainda não foi gerada. Use \"Gerar Mini Liga\" primeiro.");

        if (jogos.Any(j => j.Status != PartidaStatus.Encerrada))
            throw new InvalidOperationException("Todos os jogos da mini liga precisam estar encerrados para apurar os classificados.");

        // Classificação parcial: apenas confrontos diretos entre os empatados
        var tabela = empatados.ToDictionary(id => id, _ => (Pts: 0, SG: 0, GP: 0));
        foreach (var j in jogos)
        {
            if (!tabela.ContainsKey(j.TimeCasaId) || !tabela.ContainsKey(j.TimeForaId)) continue;
            var (ptsCasa, ptsFora) = j.GolsCasa > j.GolsFora ? (3, 0)
                                   : j.GolsCasa < j.GolsFora ? (0, 3) : (1, 1);
            var c = tabela[j.TimeCasaId];
            tabela[j.TimeCasaId] = (c.Pts + ptsCasa, c.SG + (j.GolsCasa - j.GolsFora), c.GP + j.GolsCasa);
            var f = tabela[j.TimeForaId];
            tabela[j.TimeForaId] = (f.Pts + ptsFora, f.SG + (j.GolsFora - j.GolsCasa), f.GP + j.GolsFora);
        }

        // 2 melhores avançam para o jogo decisivo (desempate por Pts, SG, GP e, por fim, Id p/ determinismo)
        var top2 = tabela
            .OrderByDescending(kv => kv.Value.Pts)
            .ThenByDescending(kv => kv.Value.SG)
            .ThenByDescending(kv => kv.Value.GP)
            .ThenBy(kv => kv.Key)
            .Take(2)
            .Select(kv => kv.Key)
            .ToList();

        if (top2.Count < 2)
            throw new InvalidOperationException("Não foi possível determinar os 2 classificados da mini liga.");

        await CriarJogoDecisivoAsync(ligaId, top2[0], top2[1], ct);

        liga.Status = LigaStatus.DecisaoCampeao;
        liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);
        return ToDto(liga);
    }

    // ── Bracket advancement ───────────────────────────────────────────────────

    private async Task AvancarBracketAsync(LigaKnockoutJogo jogo, Guid vencedorId, CancellationToken ct)
    {
        var perdedorId = jogo.TimeCasaId == vencedorId ? jogo.TimeForaId!.Value : jogo.TimeCasaId!.Value;

        switch (jogo.Fase)
        {
            case FaseKnockout.PlayIn_A:
                // Vencedor → PlayIn_C (casa), Perdedor → eliminado
                await SetKnockoutSlotAsync(jogo.LigaId, FaseKnockout.PlayIn_C, timeCasaId: vencedorId, ct: ct);
                break;

            case FaseKnockout.PlayIn_B:
                // Vencedor → QF2 (fora), Perdedor → PlayIn_C (fora)
                await SetKnockoutSlotAsync(jogo.LigaId, FaseKnockout.QF2, timeForaId: vencedorId, ct: ct);
                await SetKnockoutSlotAsync(jogo.LigaId, FaseKnockout.PlayIn_C, timeForaId: perdedorId, ct: ct);
                break;

            case FaseKnockout.PlayIn_C:
                // Vencedor → QF1 (fora), Perdedor → eliminado
                await SetKnockoutSlotAsync(jogo.LigaId, FaseKnockout.QF1, timeForaId: vencedorId, ct: ct);
                break;

            case FaseKnockout.QF1:
                await SetKnockoutSlotAsync(jogo.LigaId, FaseKnockout.Semi1, timeCasaId: vencedorId, ct: ct);
                break;

            case FaseKnockout.QF4:
                await SetKnockoutSlotAsync(jogo.LigaId, FaseKnockout.Semi1, timeForaId: vencedorId, ct: ct);
                break;

            case FaseKnockout.QF2:
                await SetKnockoutSlotAsync(jogo.LigaId, FaseKnockout.Semi2, timeCasaId: vencedorId, ct: ct);
                break;

            case FaseKnockout.QF3:
                await SetKnockoutSlotAsync(jogo.LigaId, FaseKnockout.Semi2, timeForaId: vencedorId, ct: ct);
                break;

            case FaseKnockout.Semi1:
                await SetKnockoutSlotAsync(jogo.LigaId, FaseKnockout.Final, timeCasaId: vencedorId, ct: ct);
                break;

            case FaseKnockout.Semi2:
                await SetKnockoutSlotAsync(jogo.LigaId, FaseKnockout.Final, timeForaId: vencedorId, ct: ct);
                break;

            case FaseKnockout.Final:
                // Encerra liga e registra o campeão (vencedor da final).
                var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == jogo.LigaId, ct);
                if (liga is not null)
                {
                    liga.Status = LigaStatus.Encerrada;
                    liga.CampeaoTimeId = vencedorId;
                    liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;
                    await _db.SaveChangesAsync(ct);
                }
                break;
        }
    }

    private async Task SetKnockoutSlotAsync(Guid ligaId, FaseKnockout fase, Guid? timeCasaId = null, Guid? timeForaId = null, CancellationToken ct = default)
    {
        var jogo = await _db.LigaKnockoutJogos.FirstOrDefaultAsync(x => x.LigaId == ligaId && x.Fase == fase, ct);
        if (jogo is null) return;

        if (timeCasaId.HasValue) jogo.TimeCasaId = timeCasaId;
        if (timeForaId.HasValue) jogo.TimeForaId = timeForaId;

        await _db.SaveChangesAsync(ct);
    }

    // ── Classificação (recálculo) ─────────────────────────────────────────────

    public async Task RecalcularClassificacaoAsync(Guid rodadaId, CancellationToken ct)
    {
        var rodada = await _db.LigaRodadas.AsNoTracking().FirstOrDefaultAsync(x => x.RodadaId == rodadaId, ct);
        if (rodada is null) return;

        var ligaId = rodada.LigaId;

        // Busca apenas partidas da fase regular: Numero > 0 exclui a rodada de knockout (0) e o
        // jogo decisivo da Liga (-1); Desempate exclui o jogo decisivo da Copa, que não dá pontos.
        var partidas = await _db.LigaPartidas
            .AsNoTracking()
            .Where(x => x.Rodada.LigaId == ligaId && x.Rodada.Numero > 0 && !x.Rodada.Desempate
                        && x.Status != PartidaStatus.Agendada)
            .Include(x => x.Eventos)
            .ToListAsync(ct);

        // Busca punicoes
        var punicoes = await _db.LigaPunicoes
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .GroupBy(x => x.TimeId)
            .ToDictionaryAsync(g => g.Key, g => g.Sum(p => p.PontosSubtraidos), ct);

        // Agrupa por time
        var stats = new Dictionary<Guid, (int Pts, int J, int V, int E, int D, int GP, int GC, int CA, int CV)>();

        foreach (var p in partidas)
        {
            EnsureEntry(stats, p.TimeCasaId);
            EnsureEntry(stats, p.TimeForaId);

            var (ptsCasa, ptsForaT) = p.GolsCasa > p.GolsFora ? (3, 0) :
                                       p.GolsCasa < p.GolsFora ? (0, 3) : (1, 1);

            var c = stats[p.TimeCasaId];
            stats[p.TimeCasaId] = (
                c.Pts + ptsCasa,
                c.J + 1,
                c.V + (ptsCasa == 3 ? 1 : 0),
                c.E + (ptsCasa == 1 ? 1 : 0),
                c.D + (ptsCasa == 0 ? 1 : 0),
                c.GP + p.GolsCasa,
                c.GC + p.GolsFora,
                c.CA,
                c.CV);

            var f = stats[p.TimeForaId];
            stats[p.TimeForaId] = (
                f.Pts + ptsForaT,
                f.J + 1,
                f.V + (ptsForaT == 3 ? 1 : 0),
                f.E + (ptsForaT == 1 ? 1 : 0),
                f.D + (ptsForaT == 0 ? 1 : 0),
                f.GP + p.GolsFora,
                f.GC + p.GolsCasa,
                f.CA,
                f.CV);

            // Cartões
            foreach (var ev in p.Eventos)
            {
                if (!stats.ContainsKey(ev.TimeId)) continue;
                var t = stats[ev.TimeId];
                stats[ev.TimeId] = ev.Tipo switch
                {
                    TipoEvento.CartaoAmarelo => t with { CA = t.CA + 1 },
                    TipoEvento.CartaoVermelho => t with { CV = t.CV + 1 },
                    _ => t
                };
            }
        }

        // Atualiza entidades de classificação
        var classifs = await _db.LigaClassificacoes.Where(x => x.LigaId == ligaId).ToListAsync(ct);

        foreach (var cls in classifs)
        {
            if (stats.TryGetValue(cls.TimeId, out var s))
            {
                var desconto = punicoes.GetValueOrDefault(cls.TimeId, 0);
                cls.Pontos = s.Pts - desconto;
                cls.Jogos = s.J;
                cls.Vitorias = s.V;
                cls.Empates = s.E;
                cls.Derrotas = s.D;
                cls.GolsPro = s.GP;
                cls.GolsContra = s.GC;
                cls.CartoesAmarelos = s.CA;
                cls.CartoesVermelhos = s.CV;
            }
            else
            {
                cls.Pontos = 0; cls.Jogos = 0; cls.Vitorias = 0; cls.Empates = 0;
                cls.Derrotas = 0; cls.GolsPro = 0; cls.GolsContra = 0;
                cls.CartoesAmarelos = 0; cls.CartoesVermelhos = 0;
            }
        }

        // Times que ainda não têm entrada (caso IniciarPrimeiraFase não tenha sido chamado)
        foreach (var (timeId, s) in stats)
        {
            if (!classifs.Any(c => c.TimeId == timeId))
            {
                var desconto = punicoes.GetValueOrDefault(timeId, 0);
                _db.LigaClassificacoes.Add(new LigaClassificacao
                {
                    ClassificacaoId = Guid.NewGuid(),
                    LigaId = ligaId,
                    TimeId = timeId,
                    Pontos = s.Pts - desconto,
                    Jogos = s.J,
                    Vitorias = s.V,
                    Empates = s.E,
                    Derrotas = s.D,
                    GolsPro = s.GP,
                    GolsContra = s.GC,
                    CartoesAmarelos = s.CA,
                    CartoesVermelhos = s.CV
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        await RecalcularPosicoesAsync(ligaId, ct);
    }

    private async Task RecalcularPosicoesAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(x => x.LigaId == ligaId, ct);
        if (liga is null) return;

        var classifs = await _db.LigaClassificacoes.Where(x => x.LigaId == ligaId).ToListAsync(ct);
        var confrontos = await CarregarConfrontosAsync(ligaId, ct);

        var decisivos = await CarregarJogosDecisivosAsync(ligaId, ct);

        List<LigaClassificacao> resultado;

        if (liga.Tipo == TipoCompetition.Copa)
        {
            // Copa: cada grupo é ordenado isoladamente — a posição exibida é a do grupo.
            resultado = new List<LigaClassificacao>(classifs.Count);
            foreach (var grupo in classifs.Select(c => c.Grupo).Distinct().OrderBy(g => g))
            {
                resultado.AddRange(LigaDesempate.Ordenar(
                    classifs.Where(c => c.Grupo == grupo), liga.Tipo, c => c.TimeId, Stats, confrontos, decisivos));
            }
        }
        else
        {
            // Jogo decisivo de zona (rodada de desempate) separa quem segue igual após o confronto direto.
            resultado = LigaDesempate.Ordenar(classifs, liga.Tipo, c => c.TimeId, Stats, confrontos, decisivos);
            await AplicarDecisaoDeTituloAsync(ligaId, resultado, ct);
        }

        for (int j = 0; j < resultado.Count; j++)
            resultado[j].Posicao = j + 1;

        await _db.SaveChangesAsync(ct);

        static DesempateStats Stats(LigaClassificacao c) =>
            new(c.Pontos, c.Vitorias, c.SaldoGols, c.GolsPro);
    }

    /// <summary>
    /// Jogo decisivo do título (Numero = -1) encerrado: vencedor em 1º e perdedor em 2º, mesmo que o
    /// confronto direto dissesse outra coisa — na Série B isso decide acesso direto x playoff.
    /// </summary>
    private async Task AplicarDecisaoDeTituloAsync(Guid ligaId, List<LigaClassificacao> ordenados, CancellationToken ct)
    {
        var jogo = await _db.LigaPartidas
            .AsNoTracking()
            .Where(p => p.Rodada.LigaId == ligaId && p.Rodada.Numero == NumeroJogoDecisivo && p.Status == PartidaStatus.Encerrada)
            .FirstOrDefaultAsync(ct);
        if (jogo is null) return;

        var vencedorId = LigaDesempate.VencedorDoJogoDecisivo(
            jogo.TimeCasaId, jogo.TimeForaId, jogo.GolsCasa, jogo.GolsFora, jogo.TemPenaltis, jogo.PenaltisVencedorId);
        if (vencedorId is not Guid v) return;

        var perdedorId = v == jogo.TimeCasaId ? jogo.TimeForaId : jogo.TimeCasaId;
        var vencedor = ordenados.FirstOrDefault(c => c.TimeId == v);
        var perdedor = ordenados.FirstOrDefault(c => c.TimeId == perdedorId);
        if (vencedor is null || perdedor is null) return;

        ordenados.Remove(vencedor);
        ordenados.Remove(perdedor);
        ordenados.InsertRange(0, new[] { vencedor, perdedor });
    }

    private static void EnsureEntry(Dictionary<Guid, (int, int, int, int, int, int, int, int, int)> d, Guid id)
    {
        if (!d.ContainsKey(id)) d[id] = (0, 0, 0, 0, 0, 0, 0, 0, 0);
    }


    /// <summary>
    /// Leva o confronto escolhido para a última rodada trocando a rodada dele de lugar com a
    /// última. Como cada rodada é um conjunto completo de jogos, a troca mantém a tabela válida.
    /// </summary>
    private static void PorConfrontoFinal(List<List<(Guid Casa, Guid Fora)>> jogos, Guid? timeA, Guid? timeB)
    {
        if (timeA is not Guid a || timeB is not Guid b || a == b || jogos.Count < 2) return;

        var atual = jogos.FindIndex(rodada => rodada.Any(j =>
            (j.Casa == a && j.Fora == b) || (j.Casa == b && j.Fora == a)));

        if (atual < 0 || atual == jogos.Count - 1) return;

        (jogos[atual], jogos[^1]) = (jogos[^1], jogos[atual]);
    }

    private static List<List<(Guid, Guid)>> GerarRoundRobinParcial(List<Guid> times, int totalRodadas)
    {
        var n = times.Count;
        if (n % 2 != 0)
        {
            times = new List<Guid>(times) { Guid.Empty }; // bye
            n++;
        }

        var resultado = new List<List<(Guid, Guid)>>();
        var fixo = times[0];
        var rotacao = times.Skip(1).ToList();

        for (int r = 0; r < Math.Min(totalRodadas, n - 1); r++)
        {
            var rodada = new List<(Guid, Guid)>();
            var atual = new[] { fixo }.Concat(rotacao).ToArray();

            for (int i = 0; i < n / 2; i++)
            {
                var casa = atual[i];
                var fora = atual[n - 1 - i];
                if (casa != Guid.Empty && fora != Guid.Empty)
                    rodada.Add(r % 2 == 0 ? (casa, fora) : (fora, casa));
            }

            resultado.Add(rodada);
            rotacao = new List<Guid> { rotacao[^1] }.Concat(rotacao.Take(rotacao.Count - 1)).ToList();
        }

        return resultado;
    }

    private async Task<LigaPartidaDto> GetPartidaDtoAsync(Guid partidaId, CancellationToken ct)
    {
        var p = await _db.LigaPartidas
            .AsNoTracking()
            .Include(x => x.Rodada)
            .Include(x => x.TimeCasa)
            .Include(x => x.TimeFora)
            .FirstAsync(x => x.PartidaId == partidaId, ct);

        return ToPartidaDto(p);
    }

    private async Task<LigaEventoDto> GetEventoDtoAsync(Guid eventoId, CancellationToken ct)
    {
        var ev = await _db.LigaEventos
            .AsNoTracking()
            .Include(x => x.Time)
            .Include(x => x.Jogador)
            .Include(x => x.Assistente)
            .Include(x => x.JogadorSaiu)
            .FirstAsync(x => x.EventoId == eventoId, ct);

        return ToEventoDto(ev);
    }

    private async Task<IReadOnlyList<LigaKnockoutJogoDto>> GetKnockoutDtosAsync(Guid ligaId, CancellationToken ct)
    {
        var jogos = await _db.LigaKnockoutJogos
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .Include(x => x.TimeCasa)
            .Include(x => x.TimeFora)
            .Include(x => x.Vencedor)
            .Include(x => x.Partida)
            .OrderBy(x => x.Fase)
            .ToListAsync(ct);

        return jogos.Select(ToKnockoutJogoDto).ToArray();
    }

    private async Task<LigaKnockoutJogoDto> GetKnockoutJogoDtoAsync(Guid knockoutJogoId, CancellationToken ct)
    {
        var jogo = await _db.LigaKnockoutJogos
            .AsNoTracking()
            .Include(x => x.TimeCasa)
            .Include(x => x.TimeFora)
            .Include(x => x.Vencedor)
            .Include(x => x.Partida)
            .FirstAsync(x => x.KnockoutJogoId == knockoutJogoId, ct);

        return ToKnockoutJogoDto(jogo);
    }

    private static LigaDto ToDto(Liga l) =>
        new(l.LigaId, l.Nome, l.TotalRodadas, l.DataInicio, l.DataFim, l.Status, l.Tipo, l.CriadoEm, l.AtualizadoEm,
            l.CampeaoTimeId, l.Campeao?.TeamName, l.Temporada, l.Divisao, l.VagasDiretas, l.VagasPlayoff,
            l.ConfrontoFinalTimeAId, l.ConfrontoFinalTimeBId);

    private static LigaPartidaDto ToPartidaDto(LigaPartida p) =>
        new(p.PartidaId, p.RodadaId, p.Rodada?.Numero ?? 0, p.TimeCasaId, p.TimeCasa?.TeamName ?? "?", p.TimeForaId, p.TimeFora?.TeamName ?? "?",
            p.GolsCasa, p.GolsFora, p.Status, p.IsWO, p.TemPenaltis, p.PenaltisVencedorId, p.IniciadaEm, p.EncerradaEm);

    private static LigaEventoDto ToEventoDto(LigaEventoPartida ev) =>
        new(ev.EventoId, ev.PartidaId, ev.Tipo, ev.TimeId, ev.Time?.TeamName ?? "?", ev.JogadorId, ev.Jogador?.Name ?? "?",
            ev.AssistenteId, ev.Assistente?.Name, ev.Minuto, ev.CriadoEm, ev.JogadorSaiuId, ev.JogadorSaiu?.Name);

    private static LigaKnockoutJogoDto ToKnockoutJogoDto(LigaKnockoutJogo j) =>
        new(j.KnockoutJogoId, j.Fase, FaseLabelMap[j.Fase], j.TimeCasaId, j.TimeCasa?.TeamName,
            j.TimeForaId, j.TimeFora?.TeamName, j.VencedorId, j.Vencedor?.TeamName,
            j.PartidaId, j.Partida?.GolsCasa, j.Partida?.GolsFora,
            j.Partida is null ? null : (PartidaStatus?)j.Partida.Status,
            j.Partida?.TemPenaltis ?? false);

    private static readonly Dictionary<FaseKnockout, string> FaseLabelMap = new()
    {
        [FaseKnockout.PlayIn_A] = "Play-In Jogo 1 (9º vs 10º)",
        [FaseKnockout.PlayIn_B] = "Play-In Jogo 2 (7º vs 8º)",
        [FaseKnockout.PlayIn_C] = "Play-In Jogo 3 (Decisivo)",
        [FaseKnockout.QF1] = "Quartas 1 — 1º Grupo A x 2º Grupo B",
        [FaseKnockout.QF2] = "Quartas 2 — 1º Grupo B x 2º Grupo A",
        [FaseKnockout.QF3] = "Quartas 3 — 1º Grupo C x 2º Grupo D",
        [FaseKnockout.QF4] = "Quartas 4 — 1º Grupo D x 2º Grupo C",
        [FaseKnockout.Semi1] = "Semifinal 1",
        [FaseKnockout.Semi2] = "Semifinal 2",
        [FaseKnockout.Final] = "Final"
    };
}
