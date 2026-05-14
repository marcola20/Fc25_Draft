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
            TotalRodadas = 8,
            DataInicio = request.DataInicio,
            DataFim = request.DataFim,
            Status = LigaStatus.Criada,
            CriadoEm = now,
            AtualizadoEm = now
        };

        _db.Ligas.Add(liga);
        await _db.SaveChangesAsync(ct);
        return ToDto(liga);
    }

    public async Task<LigaDto?> GetByIdAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(x => x.LigaId == ligaId, ct);
        return liga is null ? null : ToDto(liga);
    }

    public async Task<IReadOnlyList<LigaDto>> ListAsync(CancellationToken ct)
    {
        var list = await _db.Ligas.AsNoTracking().OrderByDescending(x => x.CriadoEm).ToListAsync(ct);
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

        // Inicializa classificação para todos os times
        var times = await _db.Teams.AsNoTracking().ToListAsync(ct);
        foreach (var time in times)
        {
            if (!await _db.LigaClassificacoes.AnyAsync(x => x.LigaId == ligaId && x.TimeId == time.TeamId, ct))
            {
                _db.LigaClassificacoes.Add(new LigaClassificacao
                {
                    ClassificacaoId = Guid.NewGuid(),
                    LigaId = ligaId,
                    TimeId = time.TeamId
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(liga);
    }

    public async Task<LigaDto> EncerrarPrimeiraFaseAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == ligaId, ct)
            ?? throw new InvalidOperationException("Liga não encontrada.");

        if (liga.Status != LigaStatus.PrimeiraFase)
            throw new InvalidOperationException("Liga não está na primeira fase.");

        liga.Status = LigaStatus.PlayIn;
        liga.AtualizadoEm = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);
        return ToDto(liga);
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

        var times = await _db.Teams.AsNoTracking().Select(t => t.TeamId).ToListAsync(ct);
        if (times.Count < 2)
            throw new InvalidOperationException("Precisa de ao menos 2 times para gerar rodadas.");

        var jogos = GerarRoundRobinParcial(times, liga.TotalRodadas);

        var rodadasCriadas = new List<LigaRodada>();
        for (int r = 0; r < liga.TotalRodadas; r++)
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

        var punicao = new LigaPunicao
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

        // Aplica desconto na classificação
        var classif = await _db.LigaClassificacoes.FirstOrDefaultAsync(x => x.LigaId == ligaId && x.TimeId == request.TimeId, ct);
        if (classif is not null)
        {
            classif.Pontos = classif.Pontos - request.PontosSubtraidos;
            await _db.SaveChangesAsync(ct);
            await RecalcularPosicoesAsync(ligaId, ct);
        }

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
        await _db.SaveChangesAsync(ct);

        // Avança vencedor (e perdedor para PlayIn_C) no bracket
        await AvancarBracketAsync(jogo, vencedorId, ct);

        return await GetKnockoutJogoDtoAsync(knockoutJogoId, ct);
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
                // Encerra liga
                var liga = await _db.Ligas.FirstOrDefaultAsync(x => x.LigaId == jogo.LigaId, ct);
                if (liga is not null)
                {
                    liga.Status = LigaStatus.Encerrada;
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
        new(l.LigaId, l.Nome, l.TotalRodadas, l.DataInicio, l.DataFim, l.Status, l.CriadoEm, l.AtualizadoEm);

    private static LigaPartidaDto ToPartidaDto(LigaPartida p) =>
        new(p.PartidaId, p.RodadaId, p.Rodada.Numero, p.TimeCasaId, p.TimeCasa.TeamName, p.TimeForaId, p.TimeFora.TeamName,
            p.GolsCasa, p.GolsFora, p.Status, p.IsWO, p.TemPenaltis, p.PenaltisVencedorId, p.IniciadaEm, p.EncerradaEm);

    private static LigaEventoDto ToEventoDto(LigaEventoPartida ev) =>
        new(ev.EventoId, ev.PartidaId, ev.Tipo, ev.TimeId, ev.Time.TeamName, ev.JogadorId, ev.Jogador.Name,
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
