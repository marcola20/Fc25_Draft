using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// As pessoas da liga e por onde passaram. O nome do treinador e do auxiliar que aparece no
/// time sai daqui: sempre que uma passagem muda, o cadastro do clube acompanha.
/// </summary>
public class TreinadorService : ITreinadorService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public TreinadorService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<TreinadorDto>> ListAsync(bool incluirInativos, CancellationToken ct)
    {
        var query = _db.Treinadores.AsNoTracking().Include(t => t.Passagens).ThenInclude(p => p.Time).AsQueryable();
        if (!incluirInativos) query = query.Where(t => t.Ativo);

        var treinadores = await query.OrderBy(t => t.Nome).ToListAsync(ct);
        return treinadores.Select(ToDto).ToArray();
    }

    public async Task<TreinadorDto?> GetAsync(Guid treinadorId, CancellationToken ct)
    {
        var treinador = await _db.Treinadores.AsNoTracking()
            .Include(t => t.Passagens).ThenInclude(p => p.Time)
            .FirstOrDefaultAsync(t => t.TreinadorId == treinadorId, ct);

        return treinador is null ? null : ToDto(treinador);
    }

    public async Task<TreinadorDto?> GetPorTokenAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var limpo = token.Trim();
        var treinador = await _db.Treinadores.AsNoTracking()
            .Include(t => t.Passagens).ThenInclude(p => p.Time)
            .FirstOrDefaultAsync(t => t.Token.ToUpper() == limpo.ToUpper(), ct);

        return treinador is null ? null : ToDto(treinador);
    }

    public async Task<TreinadorDto> SalvarAsync(TreinadorSalvarRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
            throw new InvalidOperationException("Informe o nome do treinador.");

        var token = string.IsNullOrWhiteSpace(request.Token) ? GerarToken(request.Nome) : request.Token.Trim();

        var repetido = await _db.Treinadores
            .AnyAsync(t => t.Token.ToUpper() == token.ToUpper() && t.TreinadorId != request.TreinadorId, ct);
        if (repetido)
            throw new InvalidOperationException("Já existe alguém com esse token.");

        Treinador treinador;
        if (request.TreinadorId is Guid id)
        {
            treinador = await _db.Treinadores.Include(t => t.Passagens)
                .FirstOrDefaultAsync(t => t.TreinadorId == id, ct)
                ?? throw new InvalidOperationException("Treinador não encontrado.");
        }
        else
        {
            treinador = new Treinador { TreinadorId = Guid.NewGuid(), CriadoEm = _time.GetUtcNow().UtcDateTime };
            _db.Treinadores.Add(treinador);
        }

        treinador.Nome = request.Nome.Trim();
        treinador.Token = token;
        treinador.Ativo = request.Ativo;

        await _db.SaveChangesAsync(ct);
        await AtualizarNomesDosTimesAsync(treinador.Passagens.Select(p => p.TimeId), ct);

        return (await GetAsync(treinador.TreinadorId, ct))!;
    }

    public async Task<TreinadorDto> RegerarTokenAsync(Guid treinadorId, CancellationToken ct)
    {
        var treinador = await _db.Treinadores
            .FirstOrDefaultAsync(t => t.TreinadorId == treinadorId, ct)
            ?? throw new InvalidOperationException("Treinador não encontrado.");

        treinador.Token = GerarToken(treinador.Nome);
        await _db.SaveChangesAsync(ct);

        return (await GetAsync(treinadorId, ct))!;
    }

    public async Task ExcluirAsync(Guid treinadorId, CancellationToken ct)
    {
        var treinador = await _db.Treinadores.Include(t => t.Passagens)
            .FirstOrDefaultAsync(t => t.TreinadorId == treinadorId, ct)
            ?? throw new InvalidOperationException("Treinador não encontrado.");

        var times = treinador.Passagens.Select(p => p.TimeId).ToList();
        _db.Treinadores.Remove(treinador);
        await _db.SaveChangesAsync(ct);
        await AtualizarNomesDosTimesAsync(times, ct);
    }

    public async Task<TreinadorDto> RegistrarPassagemAsync(TreinadorPassagemRequest request, CancellationToken ct)
    {
        var treinador = await _db.Treinadores.Include(t => t.Passagens)
            .FirstOrDefaultAsync(t => t.TreinadorId == request.TreinadorId, ct)
            ?? throw new InvalidOperationException("Treinador não encontrado.");

        var time = await _db.Teams.FirstOrDefaultAsync(t => t.TeamId == request.TimeId, ct)
            ?? throw new InvalidOperationException("Time não encontrado.");

        if (request.Ate is DateTime fim && fim < request.Desde)
            throw new InvalidOperationException("A saída não pode ser antes da entrada.");

        // Ninguém em dois clubes ao mesmo tempo: os períodos não podem se cruzar.
        var conflito = treinador.Passagens.FirstOrDefault(p => SePisam(p.Desde, p.Ate, request.Desde, request.Ate));
        if (conflito is not null)
        {
            var nomeDoOutro = await _db.Teams.Where(t => t.TeamId == conflito.TimeId).Select(t => t.TeamName).FirstAsync(ct);
            throw new InvalidOperationException(
                $"{treinador.Nome} já está no {nomeDoOutro} nesse período (desde {conflito.Desde:dd/MM/yyyy}). Encerre essa passagem antes.");
        }

        // Cada clube tem um treinador e um auxiliar por vez.
        var ocupado = await _db.TreinadorPassagens
            .Where(p => p.TimeId == request.TimeId && p.Papel == request.Papel && p.TreinadorId != request.TreinadorId)
            .ToListAsync(ct);

        var jaTem = ocupado.FirstOrDefault(p => SePisam(p.Desde, p.Ate, request.Desde, request.Ate));
        if (jaTem is not null)
        {
            var nome = await _db.Treinadores.Where(t => t.TreinadorId == jaTem.TreinadorId).Select(t => t.Nome).FirstAsync(ct);
            throw new InvalidOperationException($"O {time.TeamName} já tem {nome} como {Rotulo(request.Papel)} nesse período.");
        }

        _db.TreinadorPassagens.Add(new TreinadorPassagem
        {
            PassagemId = Guid.NewGuid(),
            TreinadorId = request.TreinadorId,
            TimeId = request.TimeId,
            Papel = request.Papel,
            Desde = request.Desde.Date,
            Ate = request.Ate?.Date
        });

        await _db.SaveChangesAsync(ct);
        await AtualizarNomesDosTimesAsync(new[] { request.TimeId }, ct);

        return (await GetAsync(request.TreinadorId, ct))!;
    }

    public async Task<TreinadorDto> EncerrarPassagemAsync(Guid passagemId, DateTime ate, CancellationToken ct)
    {
        var passagem = await _db.TreinadorPassagens.FirstOrDefaultAsync(p => p.PassagemId == passagemId, ct)
            ?? throw new InvalidOperationException("Passagem não encontrada.");

        if (ate.Date < passagem.Desde.Date)
            throw new InvalidOperationException("A saída não pode ser antes da entrada.");

        passagem.Ate = ate.Date;
        await _db.SaveChangesAsync(ct);
        await AtualizarNomesDosTimesAsync(new[] { passagem.TimeId }, ct);

        return (await GetAsync(passagem.TreinadorId, ct))!;
    }

    public async Task RemoverPassagemAsync(Guid passagemId, CancellationToken ct)
    {
        var passagem = await _db.TreinadorPassagens.FirstOrDefaultAsync(p => p.PassagemId == passagemId, ct)
            ?? throw new InvalidOperationException("Passagem não encontrada.");

        _db.TreinadorPassagens.Remove(passagem);
        await _db.SaveChangesAsync(ct);
        await AtualizarNomesDosTimesAsync(new[] { passagem.TimeId }, ct);
    }

    public async Task<TreinadorCarreiraDto?> GetCarreiraAsync(Guid treinadorId, CancellationToken ct)
    {
        var treinador = await GetAsync(treinadorId, ct);
        if (treinador is null) return null;

        // Uma temporada entra na carreira quando a passagem cobre a competição.
        var ligas = await _db.Ligas.AsNoTracking()
            .Where(l => l.Temporada != null)
            .Select(l => new { l.LigaId, l.Nome, l.Tipo, l.Divisao, l.Status, l.Temporada, l.DataInicio, l.CampeaoTimeId, l.VagasDiretas, l.VagasPlayoff })
            .ToListAsync(ct);

        var classificacoes = await _db.LigaClassificacoes.AsNoTracking()
            .Select(c => new { c.LigaId, c.TimeId, c.Posicao })
            .ToListAsync(ct);

        var totalPorLiga = classificacoes.GroupBy(c => c.LigaId).ToDictionary(g => g.Key, g => g.Count());

        // A galeria sai do Hall of Fame, que é onde os títulos ficam registrados de verdade
        // (inclusive os das temporadas antigas). O troféu na tabela de temporadas continua
        // vindo da liga, para marcar a linha do ano em que ele foi campeão.
        var doHallOfFame = await _db.HallOfFame.AsNoTracking()
            .Where(e => e.TreinadorId == treinadorId)
            .OrderByDescending(e => e.Ano)
            .Select(e => new { e.Descricao, e.Temporada, e.Ano, e.TimeCampeao })
            .ToListAsync(ct);

        var titulos = doHallOfFame
            .Select(e => $"{e.Temporada ?? e.Ano?.ToString() ?? "—"} · {e.Descricao} ({e.TimeCampeao})")
            .ToList();

        var temporadas = new List<TreinadorTemporadaDto>();

        foreach (var passagem in treinador.Passagens)
        {
            foreach (var liga in ligas.Where(l => l.Temporada is not null))
            {
                var dentro = liga.DataInicio.Date >= passagem.Desde.Date
                             && (passagem.Ate is null || liga.DataInicio.Date <= passagem.Ate.Value.Date);
                if (!dentro) continue;

                var naLiga = classificacoes.FirstOrDefault(c => c.LigaId == liga.LigaId && c.TimeId == passagem.TimeId);
                var jogouSupercopa = liga.Tipo == TipoCompetition.Supercopa
                                     && await _db.LigaTimes.AnyAsync(t => t.LigaId == liga.LigaId && t.TimeId == passagem.TimeId, ct);

                if (naLiga is null && !jogouSupercopa) continue;

                var campeao = liga.CampeaoTimeId == passagem.TimeId;

                if (liga.Tipo != TipoCompetition.Liga) continue;

                // A posição só vale quando a competição acabou (durante a temporada ela ainda muda).
                var encerrada = liga.Status == LigaStatus.Encerrada;

                temporadas.Add(new TreinadorTemporadaDto(
                    liga.Temporada!.Value,
                    passagem.TimeId,
                    passagem.TimeNome,
                    passagem.Papel,
                    LigaLabels.Competicao(liga.Tipo, liga.Divisao),
                    encerrada && naLiga is { Posicao: > 0 } ? naLiga.Posicao : null,
                    totalPorLiga.GetValueOrDefault(liga.LigaId),
                    campeao,
                    encerrada ? null : "Em andamento"));
            }
        }

        return new TreinadorCarreiraDto(
            treinador.TreinadorId,
            treinador.Nome,
            treinador.Ativo,
            treinador.Passagens,
            temporadas.OrderByDescending(t => t.Temporada).ThenBy(t => t.Competicao).ToArray(),
            titulos);
    }

    /// <summary>Os nomes que aparecem no cadastro do time saem das passagens que estão valendo.</summary>
    private async Task AtualizarNomesDosTimesAsync(IEnumerable<Guid> timeIds, CancellationToken ct)
    {
        var ids = timeIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var times = await _db.Teams.Where(t => ids.Contains(t.TeamId)).ToListAsync(ct);
        var atuais = await _db.TreinadorPassagens.AsNoTracking()
            .Where(p => ids.Contains(p.TimeId) && p.Ate == null)
            .Select(p => new { p.TimeId, p.Papel, p.Treinador.Nome })
            .ToListAsync(ct);

        foreach (var time in times)
        {
            var doTime = atuais.Where(a => a.TimeId == time.TeamId).ToList();
            time.OwnerName = doTime.FirstOrDefault(a => a.Papel == PapelTreinador.Treinador)?.Nome ?? time.OwnerName;
            time.AuxiliarName = doTime.FirstOrDefault(a => a.Papel == PapelTreinador.Auxiliar)?.Nome;
        }

        await _db.SaveChangesAsync(ct);
    }

    private static bool SePisam(DateTime desdeA, DateTime? ateA, DateTime desdeB, DateTime? ateB) =>
        desdeA.Date <= (ateB?.Date ?? DateTime.MaxValue) && desdeB.Date <= (ateA?.Date ?? DateTime.MaxValue);

    private static string Rotulo(PapelTreinador papel) => papel == PapelTreinador.Auxiliar ? "auxiliar" : "treinador";

    private static string GerarToken(string nome)
    {
        var limpo = new string(nome.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).Take(10).ToArray());
        return $"{(limpo.Length > 0 ? limpo : "CBFV")}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }

    private static TreinadorDto ToDto(Treinador t) =>
        new(t.TreinadorId, t.Nome, t.Token, t.Ativo,
            t.Passagens
                .OrderByDescending(p => p.Ate is null)
                .ThenByDescending(p => p.Desde)
                .Select(p => new TreinadorPassagemDto(p.PassagemId, p.TimeId, p.Time?.TeamName ?? "?", p.Papel, p.Desde, p.Ate))
                .ToArray());
}
