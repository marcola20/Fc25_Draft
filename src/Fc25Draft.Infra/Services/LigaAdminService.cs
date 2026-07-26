using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
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

        if (request.Nome is not null) liga.Nome = request.Nome.Trim();
        if (request.DataInicio.HasValue) liga.DataInicio = request.DataInicio.Value;
        if (request.DataFim.HasValue) liga.DataFim = request.DataFim.Value;
        liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
        return ToDto(liga);
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

        var grupoA = grupos.Where(x => x.Grupo == GrupoCopa.A).Select(x => x.TimeId).ToList();
        var grupoB = grupos.Where(x => x.Grupo == GrupoCopa.B).Select(x => x.TimeId).ToList();

        if (grupoA.Count != 6 || grupoB.Count != 6)
            throw new InvalidOperationException("Copa requer exatamente 6 times em cada grupo.");

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

        // Gera 6 rodadas com jogos cross-group (A vs B)
        var jogos = GerarCrossGroup(grupoA, grupoB);
        for (int r = 0; r < liga.TotalRodadas; r++)
        {
            var rodada = new LigaRodada
            {
                RodadaId = Guid.NewGuid(),
                LigaId = liga.LigaId,
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
        }
    }

    /// <summary>
    /// Gera schedule cross-group: cada time de A joga contra cada time de B (36 jogos, 6 rodadas).
    /// </summary>
    private static List<List<(Guid, Guid)>> GerarCrossGroup(List<Guid> a, List<Guid> b)
    {
        // Round-robin across two groups: 6 rodadas × 6 jogos
        // Usamos rotação do grupo B mantendo A fixo para distribuir os confrontos
        var resultado = new List<List<(Guid, Guid)>>();
        var bRot = new List<Guid>(b);

        for (int r = 0; r < a.Count; r++)
        {
            var rodada = new List<(Guid, Guid)>();
            for (int i = 0; i < a.Count; i++)
            {
                if (r % 2 == 0)
                    rodada.Add((a[i], bRot[i]));
                else
                    rodada.Add((bRot[i], a[i]));
            }
            resultado.Add(rodada);
            // Rotaciona B
            bRot = new List<Guid> { bRot[^1] }.Concat(bRot.Take(bRot.Count - 1)).ToList();
        }

        return resultado;
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

        if (liga.Tipo == TipoCompetition.Copa)
        {
            // Copa vai direto para PlayIn (knockout 4 times)
            liga.Status = LigaStatus.PlayIn;
        }
        else
        {
            // Liga (pontos corridos): o campeão é o 1º colocado. Só há fase extra em caso
            // de empate no topo — jogo decisivo (2 times) ou mini liga (3+). Sem empate,
            // a competição encerra direto no líder (não há playoffs no formato da Liga).
            var classif = await _db.LigaClassificacoes
                .AsNoTracking()
                .Where(x => x.LigaId == ligaId)
                .OrderByDescending(x => x.Pontos)
                .ThenByDescending(x => x.Vitorias)
                .ThenByDescending(x => x.SaldoGols)
                .ThenByDescending(x => x.GolsPro)
                .ToListAsync(ct);

            if (classif.Count >= 2)
            {
                var lider = classif[0];
                var empatados = classif
                    .Where(c => c.Pontos == lider.Pontos &&
                                c.Vitorias == lider.Vitorias &&
                                c.SaldoGols == lider.SaldoGols &&
                                c.GolsPro == lider.GolsPro)
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

        // Remove apenas o que é gerado DEPOIS da 1ª fase (mata-mata e rodadas de desempate:
        // mini liga com Numero=0 e jogo decisivo com Numero=-1), preservando as rodadas regulares (Numero > 0).
        var knockouts = await _db.LigaKnockoutJogos.Where(x => x.LigaId == ligaId).ToListAsync(ct);
        var miniRodadas = await _db.LigaRodadas.Where(x => x.LigaId == ligaId && x.Numero <= 0).ToListAsync(ct);
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
            .Select(x => new { x.RodadaId, x.LigaId, x.Numero, Total = x.Partidas.Count })
            .ToListAsync(ct);

        return rodadas.Select(r => new LigaRodadaDto(r.RodadaId, r.LigaId, r.Numero, r.Total)).ToArray();
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

        await _db.SaveChangesAsync(ct);

        return rodadasCriadas.Select(r => new LigaRodadaDto(r.RodadaId, r.LigaId, r.Numero, r.Partidas.Count)).ToArray();
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

        if (request.AssistenteId.HasValue &&
            !await _db.Players.AnyAsync(p => p.PlayerId == request.AssistenteId.Value, ct))
            throw new InvalidOperationException("Assistente não encontrado.");

        var evento = new LigaEventoPartida
        {
            EventoId = Guid.NewGuid(),
            PartidaId = partidaId,
            Tipo = TipoEvento.Gol,
            TimeId = request.TimeId,
            JogadorId = request.JogadorId,
            AssistenteId = request.AssistenteId,
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

    public async Task DeleteEventoAsync(Guid eventoId, CancellationToken ct)
    {
        var evento = await _db.LigaEventos
            .Include(x => x.Partida)
            .FirstOrDefaultAsync(x => x.EventoId == eventoId, ct)
            ?? throw new InvalidOperationException("Evento não encontrado.");

        var partida = evento.Partida;

        if (evento.Tipo == TipoEvento.Gol)
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

    public async Task ConfigurarGruposCopaAsync(Guid ligaId, LigaConfigurarGruposRequest request, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (liga.Tipo != TipoCompetition.Copa)
            throw new InvalidOperationException("Esta liga não é uma Copa.");

        if (liga.Status != LigaStatus.Criada)
            throw new InvalidOperationException("Grupos só podem ser configurados antes de iniciar a copa.");

        if (request.TimesGrupoA.Count != 6 || request.TimesGrupoB.Count != 6)
            throw new InvalidOperationException("Cada grupo deve ter exatamente 6 times.");

        var todos = request.TimesGrupoA.Concat(request.TimesGrupoB).ToList();
        if (todos.Distinct().Count() != 12)
            throw new InvalidOperationException("Times repetidos entre os grupos.");

        // Remove grupos anteriores
        var existentes = await _db.LigaGruposTimes.Where(x => x.LigaId == ligaId).ToListAsync(ct);
        _db.LigaGruposTimes.RemoveRange(existentes);

        foreach (var timeId in request.TimesGrupoA)
            _db.LigaGruposTimes.Add(new LigaGrupoTime { Id = Guid.NewGuid(), LigaId = ligaId, TimeId = timeId, Grupo = GrupoCopa.A });

        foreach (var timeId in request.TimesGrupoB)
            _db.LigaGruposTimes.Add(new LigaGrupoTime { Id = Guid.NewGuid(), LigaId = ligaId, TimeId = timeId, Grupo = GrupoCopa.B });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ListTimesLigaAsync(Guid ligaId, CancellationToken ct)
        => await _db.LigaTimes.AsNoTracking()
            .Where(x => x.LigaId == ligaId)
            .Select(x => x.TimeId)
            .ToListAsync(ct);

    public async Task ConfigurarTimesLigaAsync(Guid ligaId, IReadOnlyList<Guid> teamIds, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (liga.Tipo != TipoCompetition.Liga)
            throw new InvalidOperationException("A inscrição de times avulsa aplica-se apenas à Liga (pontos corridos). Use os grupos na Copa.");

        if (liga.Status != LigaStatus.Criada)
            throw new InvalidOperationException("Os times só podem ser configurados antes de iniciar a liga.");

        var distinct = teamIds.Distinct().ToList();
        if (distinct.Count < 2)
            throw new InvalidOperationException("Selecione ao menos 2 times.");

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
        // Top 2 de cada grupo pela classificação
        var classifA = await _db.LigaClassificacoes
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId && x.Grupo == GrupoCopa.A)
            .OrderBy(x => x.Posicao)
            .Take(2)
            .ToListAsync(ct);

        var classifB = await _db.LigaClassificacoes
            .AsNoTracking()
            .Where(x => x.LigaId == ligaId && x.Grupo == GrupoCopa.B)
            .OrderBy(x => x.Posicao)
            .Take(2)
            .ToListAsync(ct);

        if (classifA.Count < 2 || classifB.Count < 2)
            throw new InvalidOperationException("Precisa de ao menos 2 classificados por grupo.");

        // Semi1: 1A vs 2B, Semi2: 1B vs 2A
        var jogos = new[]
        {
            new LigaKnockoutJogo { KnockoutJogoId = Guid.NewGuid(), LigaId = ligaId, Fase = FaseKnockout.Semi1, TimeCasaId = classifA[0].TimeId, TimeForaId = classifB[1].TimeId },
            new LigaKnockoutJogo { KnockoutJogoId = Guid.NewGuid(), LigaId = ligaId, Fase = FaseKnockout.Semi2, TimeCasaId = classifB[0].TimeId, TimeForaId = classifA[1].TimeId },
            new LigaKnockoutJogo { KnockoutJogoId = Guid.NewGuid(), LigaId = ligaId, Fase = FaseKnockout.Final }
        };

        _db.LigaKnockoutJogos.AddRange(jogos);
        await _db.SaveChangesAsync(ct);

        return await GetKnockoutDtosAsync(ligaId, ct);
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
        var empatados = classif
            .Where(c => c.Pontos == lider.Pontos &&
                        c.Vitorias == lider.Vitorias &&
                        c.SaldoGols == lider.SaldoGols &&
                        c.GolsPro == lider.GolsPro)
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
        var empatados = classif
            .Where(c => c.Pontos == lider.Pontos &&
                        c.Vitorias == lider.Vitorias &&
                        c.SaldoGols == lider.SaldoGols &&
                        c.GolsPro == lider.GolsPro)
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

    private async Task RecalcularClassificacaoAsync(Guid rodadaId, CancellationToken ct)
    {
        var rodada = await _db.LigaRodadas.AsNoTracking().FirstOrDefaultAsync(x => x.RodadaId == rodadaId, ct);
        if (rodada is null) return;

        var ligaId = rodada.LigaId;

        // Busca apenas partidas da fase regular (Rodada.Numero > 0 exclui rodada knockout = 0)
        var partidas = await _db.LigaPartidas
            .AsNoTracking()
            .Where(x => x.Rodada.LigaId == ligaId && x.Rodada.Numero > 0 && x.Status != PartidaStatus.Agendada)
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
                cls.SaldoGols = s.GP - s.GC;
                cls.CartoesAmarelos = s.CA;
                cls.CartoesVermelhos = s.CV;
            }
            else
            {
                cls.Pontos = 0; cls.Jogos = 0; cls.Vitorias = 0; cls.Empates = 0;
                cls.Derrotas = 0; cls.GolsPro = 0; cls.GolsContra = 0; cls.SaldoGols = 0;
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
                    SaldoGols = s.GP - s.GC,
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
        var classifs = await _db.LigaClassificacoes.Where(x => x.LigaId == ligaId).ToListAsync(ct);

        var ordenados = classifs
            .OrderByDescending(c => c.Pontos)
            .ThenByDescending(c => c.Vitorias)
            .ThenByDescending(c => c.SaldoGols)
            .ThenByDescending(c => c.GolsPro)
            .ToList();

        var partidas = await _db.LigaPartidas
            .AsNoTracking()
            .Where(p => p.Rodada.LigaId == ligaId && p.Status == PartidaStatus.Encerrada && !p.IsWO)
            .ToListAsync(ct);

        var resultado = new List<LigaClassificacao>();
        int i = 0;
        while (i < ordenados.Count)
        {
            var c = ordenados[i];
            var grupo = ordenados
                .Skip(i)
                .TakeWhile(x => x.Pontos == c.Pontos && x.Vitorias == c.Vitorias &&
                                x.SaldoGols == c.SaldoGols && x.GolsPro == c.GolsPro)
                .ToList();

            resultado.AddRange(grupo.Count > 1 ? AplicarConfrontoDireto(grupo, partidas) : grupo);
            i += grupo.Count;
        }

        for (int j = 0; j < resultado.Count; j++)
            resultado[j].Posicao = j + 1;

        await _db.SaveChangesAsync(ct);
    }

    private static List<LigaClassificacao> AplicarConfrontoDireto(
        List<LigaClassificacao> grupo,
        List<LigaPartida> todasPartidas)
    {
        var ids = grupo.Select(g => g.TimeId).ToHashSet();
        var h2h = todasPartidas
            .Where(p => ids.Contains(p.TimeCasaId) && ids.Contains(p.TimeForaId))
            .ToList();

        if (h2h.Count == 0)
            return grupo;

        var stats = grupo.ToDictionary(g => g.TimeId, _ => (Pts: 0, V: 0, SG: 0, GP: 0));

        foreach (var p in h2h)
        {
            var (cpCasa, vCasa, sgCasa, gpCasa) = stats[p.TimeCasaId];
            var (cpFora, vFora, sgFora, gpFora) = stats[p.TimeForaId];

            gpCasa += p.GolsCasa;
            gpFora += p.GolsFora;
            sgCasa += p.GolsCasa - p.GolsFora;
            sgFora += p.GolsFora - p.GolsCasa;

            if (p.GolsCasa > p.GolsFora)      { cpCasa += 3; vCasa += 1; }
            else if (p.GolsCasa == p.GolsFora) { cpCasa += 1; cpFora += 1; }
            else                               { cpFora += 3; vFora += 1; }

            stats[p.TimeCasaId] = (cpCasa, vCasa, sgCasa, gpCasa);
            stats[p.TimeForaId] = (cpFora, vFora, sgFora, gpFora);
        }

        return grupo
            .OrderByDescending(g => stats[g.TimeId].Pts)
            .ThenByDescending(g => stats[g.TimeId].V)
            .ThenByDescending(g => stats[g.TimeId].SG)
            .ThenByDescending(g => stats[g.TimeId].GP)
            .ThenBy(g => g.TimeId) 
            .ToList();
    }

    private static void EnsureEntry(Dictionary<Guid, (int, int, int, int, int, int, int, int, int)> d, Guid id)
    {
        if (!d.ContainsKey(id)) d[id] = (0, 0, 0, 0, 0, 0, 0, 0, 0);
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
            l.CampeaoTimeId, l.Campeao?.TeamName);

    private static LigaPartidaDto ToPartidaDto(LigaPartida p) =>
        new(p.PartidaId, p.RodadaId, p.Rodada?.Numero ?? 0, p.TimeCasaId, p.TimeCasa?.TeamName ?? "?", p.TimeForaId, p.TimeFora?.TeamName ?? "?",
            p.GolsCasa, p.GolsFora, p.Status, p.IsWO, p.TemPenaltis, p.PenaltisVencedorId, p.IniciadaEm, p.EncerradaEm);

    private static LigaEventoDto ToEventoDto(LigaEventoPartida ev) =>
        new(ev.EventoId, ev.PartidaId, ev.Tipo, ev.TimeId, ev.Time?.TeamName ?? "?", ev.JogadorId, ev.Jogador?.Name ?? "?",
            ev.AssistenteId, ev.Assistente?.Name, ev.Minuto, ev.CriadoEm);

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
        [FaseKnockout.QF1] = "Quartas - 1º vs Vencedor Play-In 3",
        [FaseKnockout.QF2] = "Quartas - 2º vs Vencedor Play-In 2",
        [FaseKnockout.QF3] = "Quartas - 3º vs 6º",
        [FaseKnockout.QF4] = "Quartas - 4º vs 5º",
        [FaseKnockout.Semi1] = "Semifinal 1",
        [FaseKnockout.Semi2] = "Semifinal 2",
        [FaseKnockout.Final] = "Final"
    };
}
