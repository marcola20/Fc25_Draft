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
            .Select(l => new { l.LigaId, l.Nome, l.Tipo, l.Divisao, l.Status, l.Temporada, l.DataInicio, l.DataFim, l.CampeaoTimeId, l.VagasDiretas, l.VagasPlayoff })
            .ToListAsync(ct);

        var classificacoes = await _db.LigaClassificacoes.AsNoTracking()
            .Select(c => new { c.LigaId, c.TimeId, c.Posicao })
            .ToListAsync(ct);

        var totalPorLiga = classificacoes.GroupBy(c => c.LigaId).ToDictionary(g => g.Key, g => g.Count());

        // Quando a competição acabou de verdade: o último jogo encerrado. A DataFim é só o
        // que estava previsto, e quase nunca bate com o que aconteceu.
        var ultimoJogo = await _db.LigaPartidas.AsNoTracking()
            .Where(p => p.EncerradaEm != null)
            .GroupBy(p => p.Rodada.LigaId)
            .Select(g => new { LigaId = g.Key, Fim = g.Max(p => p.EncerradaEm) })
            .ToDictionaryAsync(x => x.LigaId, x => x.Fim!.Value, ct);

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

        // Os jogos que ele comandou: só partida encerrada, e só dentro do período dele.
        var timeIds = treinador.Passagens.Select(p => p.TimeId).Distinct().ToList();

        var jogos = await _db.LigaPartidas.AsNoTracking()
            .Where(p => p.EncerradaEm != null
                        && (timeIds.Contains(p.TimeCasaId) || timeIds.Contains(p.TimeForaId)))
            .Select(p => new Jogo(p.Rodada.LigaId, p.TimeCasaId, p.TimeForaId, p.GolsCasa, p.GolsFora, p.EncerradaEm!.Value))
            .ToListAsync(ct);

        // No dia em que o comando troca, o jogo daquele dia é de quem estava saindo.
        var entradasNoDiaDaSaida = await _db.TreinadorPassagens.AsNoTracking()
            .Where(p => timeIds.Contains(p.TimeId) && p.Ate != null)
            .Select(p => new { p.TimeId, Dia = p.Ate!.Value })
            .ToListAsync(ct);

        List<Jogo> JogosDa(TreinadorPassagemDto passagem, Guid? ligaId = null)
        {
            var herdouODia = passagem.Ate is null
                             || entradasNoDiaDaSaida.Any(e => e.TimeId == passagem.TimeId && e.Dia.Date == passagem.Desde.Date);

            return jogos.Where(j =>
                (j.CasaId == passagem.TimeId || j.ForaId == passagem.TimeId)
                && (ligaId is null || j.LigaId == ligaId)
                && j.Quando.Date >= passagem.Desde.Date
                && (!herdouODia || j.Quando.Date > passagem.Desde.Date)
                && (passagem.Ate is null || j.Quando.Date <= passagem.Ate.Value.Date)).ToList();
        }

        var passagensComRetrospecto = treinador.Passagens
            .Select(p => p with { Retrospecto = Retrospecto(JogosDa(p), p.TimeId) })
            .ToArray();

        // Na Copa e na Supercopa não existe posição na tabela: o que conta é onde ele parou.
        var mataMata = await _db.LigaKnockoutJogos.AsNoTracking()
            .Select(k => new { k.LigaId, k.Fase, k.TimeCasaId, k.TimeForaId })
            .ToListAsync(ct);

        string? FaseDoTime(Guid ligaId, Guid timeId, bool campeao)
        {
            if (campeao) return "Campeão";

            var doTime = mataMata
                .Where(k => k.LigaId == ligaId && (k.TimeCasaId == timeId || k.TimeForaId == timeId))
                .ToList();

            if (doTime.Count == 0)
                return mataMata.Any(k => k.LigaId == ligaId) ? "Fase de grupos" : null;

            return doTime.Max(k => k.Fase) switch
            {
                FaseKnockout.Final => "Vice",
                FaseKnockout.Semi1 or FaseKnockout.Semi2 => "Semifinal",
                FaseKnockout.QF1 or FaseKnockout.QF2 or FaseKnockout.QF3 or FaseKnockout.QF4 => "Quartas",
                FaseKnockout.PlayIn_A or FaseKnockout.PlayIn_B or FaseKnockout.PlayIn_C => "Repescagem",
                _ => null
            };
        }

        var temporadas = new List<TreinadorTemporadaDto>();

        foreach (var passagem in treinador.Passagens)
        {
            foreach (var liga in ligas.Where(l => l.Temporada is not null))
            {
                // Vale quem estava no comando em algum momento da competição, não só quem
                // começou com ela: quem assume no meio da temporada também a disputou.
                var comecou = liga.DataInicio.Date;
                var acabou = (ultimoJogo.TryGetValue(liga.LigaId, out var fim) ? fim : liga.DataFim).Date;

                var pegou = passagem.Desde.Date < (acabou > comecou ? acabou : DateTime.MaxValue)
                            && comecou < (passagem.Ate?.Date ?? DateTime.MaxValue);
                if (!pegou) continue;

                var naLiga = classificacoes.FirstOrDefault(c => c.LigaId == liga.LigaId && c.TimeId == passagem.TimeId);
                var jogouSupercopa = liga.Tipo == TipoCompetition.Supercopa
                                     && await _db.LigaTimes.AnyAsync(t => t.LigaId == liga.LigaId && t.TimeId == passagem.TimeId, ct);

                if (naLiga is null && !jogouSupercopa) continue;

                var campeao = liga.CampeaoTimeId == passagem.TimeId;
                var naTabela = liga.Tipo == TipoCompetition.Liga;

                // A posição só vale quando a competição acabou (durante a temporada ela ainda muda).
                var encerrada = liga.Status == LigaStatus.Encerrada;

                temporadas.Add(new TreinadorTemporadaDto(
                    liga.Temporada!.Value,
                    passagem.TimeId,
                    passagem.TimeNome,
                    passagem.Papel,
                    LigaLabels.Competicao(liga.Tipo, liga.Divisao),
                    naTabela && encerrada && naLiga is { Posicao: > 0 } ? naLiga.Posicao : null,
                    naTabela ? totalPorLiga.GetValueOrDefault(liga.LigaId) : null,
                    campeao,
                    Movimento(passagem, comecou, acabou, encerrada),
                    Retrospecto(JogosDa(passagem, liga.LigaId), passagem.TimeId),
                    naTabela ? null : FaseDoTime(liga.LigaId, passagem.TimeId, campeao)));
            }
        }

        return new TreinadorCarreiraDto(
            treinador.TreinadorId,
            treinador.Nome,
            treinador.Ativo,
            passagensComRetrospecto,
            temporadas.OrderByDescending(t => t.Temporada).ThenBy(t => t.Competicao).ToArray(),
            titulos,
            Somar(passagensComRetrospecto.Select(p => p.Retrospecto!)));
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

    private record Jogo(Guid LigaId, Guid CasaId, Guid ForaId, int GolsCasa, int GolsFora, DateTime Quando);

    /// <summary>Soma os jogos olhando sempre do lado do clube que ele comandava.</summary>
    private static TreinadorRetrospectoDto Retrospecto(IEnumerable<Jogo> jogos, Guid timeId)
    {
        int j = 0, v = 0, e = 0, d = 0, gp = 0, gc = 0;

        foreach (var jogo in jogos)
        {
            var emCasa = jogo.CasaId == timeId;
            var feitos = emCasa ? jogo.GolsCasa : jogo.GolsFora;
            var sofridos = emCasa ? jogo.GolsFora : jogo.GolsCasa;

            j++;
            gp += feitos;
            gc += sofridos;

            if (feitos > sofridos) v++;
            else if (feitos < sofridos) d++;
            else e++;
        }

        return new TreinadorRetrospectoDto(j, v, e, d, gp, gc);
    }

    private static TreinadorRetrospectoDto Somar(IEnumerable<TreinadorRetrospectoDto> partes) =>
        partes.Aggregate(TreinadorRetrospectoDto.Vazio, (a, b) => new TreinadorRetrospectoDto(
            a.Jogos + b.Jogos, a.Vitorias + b.Vitorias, a.Empates + b.Empates,
            a.Derrotas + b.Derrotas, a.GolsPro + b.GolsPro, a.GolsContra + b.GolsContra));

    /// <summary>Se a pessoa pegou a temporada começada ou saiu antes do fim.</summary>
    private static string? Movimento(TreinadorPassagemDto passagem, DateTime comecou, DateTime acabou, bool encerrada)
    {
        var assumiuNoMeio = passagem.Desde.Date > comecou;
        var saiuNoMeio = passagem.Ate is DateTime saida && encerrada && saida.Date < acabou;

        if (assumiuNoMeio && saiuNoMeio) return "Assumiu e saiu no meio";
        if (assumiuNoMeio) return "Assumiu com a temporada em andamento";
        if (saiuNoMeio) return "Saiu no meio da temporada";
        return encerrada ? null : "Em andamento";
    }

    /// <summary>
    /// Os períodos se cruzam quando um começa antes do outro acabar. A data de saída é o dia
    /// em que a pessoa deixa o clube, então o substituto pode entrar nesse mesmo dia.
    /// </summary>
    private static bool SePisam(DateTime desdeA, DateTime? ateA, DateTime desdeB, DateTime? ateB) =>
        desdeA.Date < (ateB?.Date ?? DateTime.MaxValue) && desdeB.Date < (ateA?.Date ?? DateTime.MaxValue);

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
