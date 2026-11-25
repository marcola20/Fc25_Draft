using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class HallOfFameService : IHallOfFameService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public HallOfFameService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<HallOfFameEntryDto>> GetAllAsync(CancellationToken ct)
    {
        var list = await _db.HallOfFame
            .AsNoTracking()
            .OrderByDescending(x => x.Ano)
            .ThenByDescending(x => x.CriadoEm)
            .ToListAsync(ct);
        return list.Select(ToDto).ToArray();
    }

    public async Task<HallOfFameEntryDto> CreateAsync(HallOfFameCreateRequest request, CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var entry = new HallOfFameEntry
        {
            HallOfFameId = Guid.NewGuid(),
            Descricao = NormalizarDescricao(request.Descricao),
            Tipo = request.Tipo,
            Divisao = request.Tipo == TipoCompetition.Liga ? request.Divisao : null,
            TimeCampeao = NormalizarTime(request.TimeCampeao),
            Tecnico = NormalizarOpcional(request.Tecnico),
            TreinadorId = request.TreinadorId,
            Ano = request.Ano,
            Temporada = NormalizarOpcional(request.Temporada),
            TimeVice = NormalizarOpcional(request.TimeVice),
            TecnicoVice = NormalizarOpcional(request.TecnicoVice),
            TreinadorViceId = request.TreinadorViceId,
            CriadoEm = now,
            AtualizadoEm = now
        };

        _db.HallOfFame.Add(entry);
        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task<HallOfFameEntryDto> UpdateAsync(Guid id, HallOfFameUpdateRequest request, CancellationToken ct)
    {
        var entry = await _db.HallOfFame.FirstOrDefaultAsync(x => x.HallOfFameId == id, ct)
            ?? throw new InvalidOperationException("Entrada do Hall da Fama não encontrada.");

        entry.Descricao = NormalizarDescricao(request.Descricao);
        entry.Tipo = request.Tipo;
        entry.Divisao = request.Tipo == TipoCompetition.Liga ? request.Divisao : null;
        entry.TimeCampeao = NormalizarTime(request.TimeCampeao);
        entry.Tecnico = NormalizarOpcional(request.Tecnico);
        entry.TreinadorId = request.TreinadorId;
        entry.Ano = request.Ano;
        entry.Temporada = NormalizarOpcional(request.Temporada);
        entry.TimeVice = NormalizarOpcional(request.TimeVice);
        entry.TecnicoVice = NormalizarOpcional(request.TecnicoVice);
        entry.TreinadorViceId = request.TreinadorViceId;
        entry.AtualizadoEm = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var entry = await _db.HallOfFame.FirstOrDefaultAsync(x => x.HallOfFameId == id, ct)
            ?? throw new InvalidOperationException("Entrada do Hall da Fama não encontrada.");

        _db.HallOfFame.Remove(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> PreencherVicesAsync(CancellationToken ct)
    {
        var semVice = await _db.HallOfFame
            .Where(h => h.LigaId != null && h.TimeVice == null)
            .Join(_db.Ligas.Where(l => l.CampeaoTimeId != null), h => h.LigaId, l => l.LigaId, (h, l) => new { Entrada = h, Campeao = l.CampeaoTimeId!.Value })
            .ToListAsync(ct);

        var preenchidos = 0;
        foreach (var x in semVice)
        {
            var vice = await ViceDaCompeticao.CalcularAsync(_db, x.Entrada.LigaId!.Value, x.Campeao, ct);
            if (vice is null) continue;
            x.Entrada.TimeVice = vice.Time;
            x.Entrada.TecnicoVice = vice.Tecnico;
            x.Entrada.TreinadorViceId = vice.TreinadorId;
            x.Entrada.AtualizadoEm = _time.GetUtcNow().UtcDateTime;
            preenchidos++;
        }

        await _db.SaveChangesAsync(ct);
        return preenchidos;
    }

    public async Task<IReadOnlyList<TreinadorNoHallDto>> TreinadoresAsync(CancellationToken ct)
    {
        var entradas = await _db.HallOfFame.AsNoTracking().ToListAsync(ct);
        var cadastrados = await _db.Treinadores.AsNoTracking()
            .Select(t => new { t.TreinadorId, t.Nome })
            .ToListAsync(ct);
        var nomePorId = cadastrados.ToDictionary(t => t.TreinadorId, t => t.Nome);

        // Nome solto que bate com um treinador cadastrado conta para ele.
        var idPorNome = cadastrados
            .GroupBy(t => t.Nome.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First().TreinadorId, StringComparer.OrdinalIgnoreCase);

        Quem? QuemFoi(Guid? treinadorId, string? nome)
        {
            if (treinadorId is Guid id) return new Quem(id, nomePorId.GetValueOrDefault(id) ?? nome ?? "?");
            if (string.IsNullOrWhiteSpace(nome)) return null;
            var limpo = nome.Trim();
            return idPorNome.TryGetValue(limpo, out var achado) ? new Quem(achado, nomePorId[achado]) : new Quem(null, limpo);
        }

        static string Quando(HallOfFameEntry e)
        {
            var t = e.Temporada?.Trim();
            if (!string.IsNullOrEmpty(t)) return int.TryParse(t, out var n) ? $"{n}ª Temporada" : t;
            return e.Ano?.ToString() ?? "—";
        }

        var finais = new List<(Quem Quem, bool Campeao, FinalDoTreinadorDto Final, int Ano)>();
        foreach (var e in entradas)
        {
            var competicao = LigaLabels.Competicao(e.Tipo, e.Divisao);
            if (QuemFoi(e.TreinadorId, e.Tecnico) is { } campeao)
                finais.Add((campeao, true, new FinalDoTreinadorDto(competicao, Quando(e), e.TimeCampeao), e.Ano ?? 0));
            if (e.TimeVice is not null && QuemFoi(e.TreinadorViceId, e.TecnicoVice) is { } vice)
                finais.Add((vice, false, new FinalDoTreinadorDto(competicao, Quando(e), e.TimeVice), e.Ano ?? 0));
        }

        return finais
            .GroupBy(x => x.Quem.Id is Guid id ? id.ToString() : "nome:" + x.Quem.Nome.ToLowerInvariant())
            .Select(g => new TreinadorNoHallDto(
                g.First().Quem.Id,
                g.First().Quem.Nome,
                g.Where(x => x.Campeao).OrderByDescending(x => x.Ano).Select(x => x.Final).ToList(),
                g.Where(x => !x.Campeao).OrderByDescending(x => x.Ano).Select(x => x.Final).ToList()))
            .OrderByDescending(t => t.Titulos.Count)
            .ThenBy(t => t.Nome)
            .ToList();
    }

    private sealed record Quem(Guid? Id, string Nome);

    public async Task<IReadOnlyList<HallDaFamaJogadorDto>> JogadoresAsync(CancellationToken ct)
    {
        var lista = await _db.Set<HallDaFamaJogador>().AsNoTracking()
            .Select(h => new
            {
                h.HallDaFamaJogadorId, h.PlayerId, h.Player.Name, Posicao = h.Player.Position.Name, h.Player.Overall,
                h.Temporada, h.Colocacao, h.Motivo,
                Aposentado = h.Player.AposentadoNaTemporada != null,
                Clube = h.Player.AposentadoNaTemporada != null
                    ? _db.Teams.Where(t => t.TeamId == h.Player.TimeAoSeAposentar).Select(t => t.TeamName).FirstOrDefault()
                    : h.Player.CurrentTeam != null ? h.Player.CurrentTeam.TeamName : null
            })
            .ToListAsync(ct);

        var numeros = await NumerosNaLiga.CalcularAsync(_db, lista.Select(h => h.PlayerId).ToList(), ct);

        return lista
            .OrderByDescending(h => h.Temporada).ThenBy(h => h.Colocacao)
            .Select(h =>
            {
                var n = numeros.GetValueOrDefault(h.PlayerId) ?? new NumerosNaLiga.Numeros(0, 0, 0);
                return new HallDaFamaJogadorDto(h.HallDaFamaJogadorId, h.PlayerId, h.Name, h.Posicao, h.Overall,
                    h.Temporada, h.Colocacao, h.Motivo, h.Aposentado, h.Clube, n.Jogos, n.Gols, n.Assistencias);
            })
            .ToList();
    }

    public async Task IncluirJogadorAsync(HallDaFamaJogadorRequest request, CancellationToken ct)
    {
        if (request.Colocacao is < 1 or > 3)
            throw new InvalidOperationException("A colocação vai de 1 a 3.");
        if (request.Temporada < 2000)
            throw new InvalidOperationException("Informe a temporada da turma (ex.: 2010).");

        var nome = await _db.Players.AsNoTracking().Where(p => p.PlayerId == request.PlayerId).Select(p => p.Name).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Jogador não encontrado.");

        var turma = _db.Set<HallDaFamaJogador>();
        if (await turma.AnyAsync(h => h.PlayerId == request.PlayerId, ct))
            throw new InvalidOperationException($"{nome} já está no Hall da Fama.");

        var ocupado = await turma.Where(h => h.Temporada == request.Temporada && h.Colocacao == request.Colocacao)
            .Select(h => h.Player.Name).FirstOrDefaultAsync(ct);
        if (ocupado is not null)
            throw new InvalidOperationException($"O {request.Colocacao}º lugar da turma de {request.Temporada} já é de {ocupado}.");

        turma.Add(new HallDaFamaJogador
        {
            HallDaFamaJogadorId = Guid.NewGuid(),
            PlayerId = request.PlayerId,
            Temporada = request.Temporada,
            Colocacao = request.Colocacao,
            Motivo = NormalizarOpcional(request.Motivo),
            CriadoEm = _time.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoverJogadorAsync(Guid id, CancellationToken ct)
    {
        var h = await _db.Set<HallDaFamaJogador>().FirstOrDefaultAsync(x => x.HallDaFamaJogadorId == id, ct)
            ?? throw new InvalidOperationException("Jogador não está no Hall da Fama.");
        _db.Remove(h);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int?> UltimaTemporadaAsync(CancellationToken ct) =>
        await _db.Ligas.AsNoTracking().MaxAsync(l => l.Temporada, ct);

    private static string NormalizarDescricao(string descricao)
    {
        var valor = descricao?.Trim();
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException("Informe a descrição.");
        return valor;
    }

    private static string NormalizarTime(string time)
    {
        var valor = time?.Trim();
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException("Informe o time campeão.");
        return valor;
    }

    private static string? NormalizarOpcional(string? valor)
    {
        var v = valor?.Trim();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static HallOfFameEntryDto ToDto(HallOfFameEntry e) =>
        new(e.HallOfFameId, e.Descricao, e.Tipo, e.Divisao, e.TimeCampeao, e.Tecnico, e.TreinadorId, e.Ano, e.Temporada, e.CriadoEm, e.AtualizadoEm, e.LigaId is not null,
            e.TimeVice, e.TecnicoVice, e.TreinadorViceId);
}
