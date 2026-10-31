using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Treino da semana: o time escolhe até 3 jovens e o foco de cada um; quando a semana fecha, cada um ganha +1 num
/// atributo do foco, que vai para o Editor PES como evolução pendente (igual à venda rápida).
/// </summary>
public class TreinoService : ITreinoService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public TreinoService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    private DateTime AgoraBrasilia => HorarioDeBrasilia.Agora(_time);
    private DateTime SemanaAtual => TreinoDaSemana.Semana(AgoraBrasilia);

    public async Task<TreinoDoTimeDto> DoTimeAsync(Guid teamId, CancellationToken ct)
    {
        var semana = SemanaAtual;
        var limites = await LimitesAsync(teamId, ct);
        var elenco = await ElencoAsync(teamId, limites, ct);
        var daSemana = await _db.TreinosSemanais.AsNoTracking()
            .Where(t => t.TeamId == teamId && t.Semana == semana)
            .ToDictionaryAsync(t => t.PlayerId, t => t.Foco, ct);

        var inicio = await InicioDaTemporadaAsync(ct);
        var ganhos = (await _db.TreinosSemanais.AsNoTracking()
                .Where(t => t.TeamId == teamId && t.Atributo != null && t.AplicadoEm >= inicio)
                .Select(t => new { t.PlayerId, ComPadrinho = t.AtributoDoPadrinho != null })
                .ToListAsync(ct))
            .GroupBy(t => t.PlayerId)
            .ToDictionary(g => g.Key, g => g.Sum(t => TreinoCriterios.GanhoPorSemana + (t.ComPadrinho ? PadrinhoCriterios.ExtraNoTreino : 0)));

        var jogadores = elenco
            .Select(j => new TreinoJogadorDto(j.PlayerId, j.Nome, j.Posicao, j.Overall, j.Idade, j.Goleiro, j.PorQueNaoTreina,
                daSemana.TryGetValue(j.PlayerId, out var foco) ? foco : null, ganhos.GetValueOrDefault(j.PlayerId), j.Grupo))
            // Quem pode treinar primeiro, os mais novos antes.
            .OrderBy(j => j.PorQueNaoTreina is not null).ThenBy(j => j.Idade ?? 99).ThenByDescending(j => j.Overall)
            .ToList();

        var passada = semana.AddDays(-7);
        var ultima = await _db.TreinosSemanais.AsNoTracking()
            .Where(t => t.TeamId == teamId && t.Semana == passada)
            .Select(t => new TreinoResultadoDto(t.Semana, t.PlayerId, t.Player.Name, t.Foco, t.Atributo,
                t.OverallAntes, t.OverallDepois, t.SemEfeito, t.AplicadoEm, t.AtributoDoPadrinho))
            .ToListAsync(ct);

        var temporada = await TemporadaAtualAsync(ct);
        var par = await ParAtivoAsync(teamId, temporada, ct);
        var veteranos = elenco
            .Where(j => j.Idade >= PadrinhoCriterios.IdadeMinima && !j.Emprestado)
            .OrderByDescending(j => j.Overall)
            .Select(j => new PadrinhoCandidatoDto(j.PlayerId, j.Nome, j.Posicao, j.Idade, j.Overall, j.Grupo))
            .ToList();

        return new TreinoDoTimeDto(semana, TreinoDaSemana.FechaEm(semana), jogadores, ultima,
            par is null ? null : await ParaDtoAsync(par, ct), veteranos, par is null && temporada is not null,
            limites.Vagas, limites.IdadeMaxima, limites.TreinadorDeGoleiros);
    }

    public async Task SalvarAsync(Guid teamId, IReadOnlyList<TreinoEscolhaRequest> escolhas, CancellationToken ct)
    {
        var limites = await LimitesAsync(teamId, ct);
        if (escolhas.Count > limites.Vagas)
            throw new InvalidOperationException($"O treino da semana é de até {limites.Vagas} jogadores.");
        if (escolhas.Select(e => e.PlayerId).Distinct().Count() != escolhas.Count)
            throw new InvalidOperationException("O mesmo jogador foi escolhido duas vezes.");

        var elenco = (await ElencoAsync(teamId, limites, ct)).ToDictionary(j => j.PlayerId);
        foreach (var e in escolhas)
        {
            if (!elenco.TryGetValue(e.PlayerId, out var j))
                throw new InvalidOperationException("Jogador não encontrado no seu elenco.");
            if (j.PorQueNaoTreina is { } motivo)
                throw new InvalidOperationException($"{j.Nome} não pode treinar: {motivo}.");
            if (!TreinoDaSemana.Focos(j.Goleiro).Contains(e.Foco))
                throw new InvalidOperationException($"{j.Nome} não treina {TreinoDaSemana.Nome(e.Foco).ToLowerInvariant()}.");
        }

        var semana = SemanaAtual;
        var agora = _time.GetUtcNow().UtcDateTime;
        var atuais = await _db.TreinosSemanais.Where(t => t.TeamId == teamId && t.Semana == semana).ToListAsync(ct);

        _db.TreinosSemanais.RemoveRange(atuais.Where(t => escolhas.All(e => e.PlayerId != t.PlayerId)));
        foreach (var e in escolhas)
        {
            if (atuais.FirstOrDefault(t => t.PlayerId == e.PlayerId) is { } existente)
            {
                existente.Foco = e.Foco;
                continue;
            }
            _db.TreinosSemanais.Add(new TreinoSemanal
            {
                TreinoId = Guid.NewGuid(), TeamId = teamId, PlayerId = e.PlayerId, Semana = semana,
                Foco = e.Foco, EscolhidoEm = agora
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> TreinarSemanasFechadasAsync(CancellationToken ct)
    {
        var semanaAtual = SemanaAtual;
        var pendentes = await _db.TreinosSemanais
            .Include(t => t.Player).ThenInclude(p => p.Atributos)
            .Include(t => t.Team)
            .Where(t => t.AplicadoEm == null && t.Semana < semanaAtual)
            .OrderBy(t => t.Semana)
            .ToListAsync(ct);
        if (pendentes.Count == 0) return 0;

        var agora = _time.GetUtcNow().UtcDateTime;
        var noElenco = (await _db.TeamRosters.AsNoTracking()
                .Select(r => new { r.TeamId, r.PlayerId })
                .ToListAsync(ct))
            .Select(r => (r.TeamId, r.PlayerId))
            .ToHashSet();

        var temporada = await TemporadaAtualAsync(ct);
        var pares = temporada is null
            ? []
            : await _db.Apadrinhamentos.AsNoTracking().Where(p => p.Temporada == temporada).ToListAsync(ct);
        var comTreinadorDeGoleiros = await ComissaoService.TimesComAsync(_db, temporada, CargoDaComissao.TreinadorDeGoleiros, ct);

        var treinaram = 0;
        var evolucoes = new List<(TreinoSemanal Treino, EvolucaoPes Evolucao)>();
        foreach (var t in pendentes)
        {
            t.AplicadoEm = agora;
            var jogador = t.Player;
            if (!noElenco.Contains((t.TeamId, t.PlayerId)))
            {
                t.SemEfeito = "saiu do clube antes do treino";
                continue;
            }
            if (jogador.Atributos?.PosicaoPes is null)
            {
                t.SemEfeito = "sem atributos do jogo no site";
                continue;
            }

            var dto = AtributosPes.ParaDto(jogador.Atributos);
            var antes = OverallPes.Valores(dto);
            if (TreinoDaSemana.AtributoQueSobe(t.Foco, antes, jogador.Atributos.PosicaoPes.Value, dto.PeFracoUso,
                    dto.PeFracoPrecisao, t.PlayerId, t.Semana) is not int indice)
            {
                t.SemEfeito = "os atributos do foco já estão no máximo";
                continue;
            }

            var depois = antes.ToArray();
            depois[indice] = Math.Min(AtributosPes.Maximo, depois[indice] + TreinoCriterios.GanhoPorSemana);

            // Com padrinho no clube (ou, para o goleiro, treinador de goleiros), rende mais: mais um ponto no foco, de
            // novo no que mais pesa. Os dois não somam.
            int? extra = null;
            var comPadrinho = pares.Any(p => p.TeamId == t.TeamId && p.AfilhadoId == t.PlayerId && noElenco.Contains((p.TeamId, p.PadrinhoId)));
            var goleiroComTreinador = comTreinadorDeGoleiros.Contains(t.TeamId) && jogador.Atributos.PosicaoPes == 0;
            if (comPadrinho || goleiroComTreinador)
            {
                extra = TreinoDaSemana.AtributoQueSobe(t.Foco, depois, jogador.Atributos.PosicaoPes.Value, dto.PeFracoUso,
                    dto.PeFracoPrecisao, t.PlayerId, t.Semana);
                if (extra is int i) depois[i] = Math.Min(AtributosPes.Maximo, depois[i] + PadrinhoCriterios.ExtraNoTreino);
            }

            for (var i = 0; i < depois.Length; i++)
                AtributosPes.Todos[i].Set(dto, depois[i]);
            AtributosPes.Aplicar(dto, jogador.Atributos);

            t.OverallAntes = jogador.Overall;
            OverallPes.Recalcular(jogador);
            t.OverallDepois = jogador.Overall;
            t.Atributo = AtributosPes.Todos[indice].Nome;
            t.AtributoDoPadrinho = extra is int e ? AtributosPes.Todos[e].Nome : null;

            var evolucao = new EvolucaoPes
            {
                PlayerId = jogador.PlayerId,
                Motivo = $"Treino da semana ({TreinoDaSemana.Nome(t.Foco)})",
                CriadaEmUtc = agora,
                OverallAntes = t.OverallAntes.Value,
                OverallDepois = jogador.Overall,
                Mudancas = EvolucaoPes.EscreverMudancas(antes, depois),
            };
            _db.EvolucoesPes.Add(evolucao);
            evolucoes.Add((t, evolucao));
            treinaram++;
        }

        // Um aviso por time e semana com o que aconteceu.
        foreach (var grupo in pendentes.GroupBy(t => (t.TeamId, t.Semana)))
        {
            var linhas = grupo.Select(t => t.Atributo is { } atributo
                ? $"{t.Player.Name} +{TreinoCriterios.GanhoPorSemana} {atributo}"
                  + (t.AtributoDoPadrinho is { } doPadrinho
                      ? $" e +{PadrinhoCriterios.ExtraNoTreino} {doPadrinho} ({(pares.Any(p => p.TeamId == t.TeamId && p.AfilhadoId == t.PlayerId) ? "padrinho" : "treinador de goleiros")})"
                      : "")
                  + (t.OverallDepois != t.OverallAntes ? $" (overall {t.OverallAntes} → {t.OverallDepois})" : "")
                : $"{t.Player.Name}: {t.SemEfeito}");
            AvisosDoTime.Criar(_db, grupo.Key.TeamId, AvisosDoTime.Treino,
                $"🏋️ Treino da semana: {string.Join("; ", linhas)}.", $"/teams/details/{grupo.Key.TeamId}", agora);
        }

        await _db.SaveChangesAsync(ct);
        foreach (var (treino, evolucao) in evolucoes)
            treino.EvolucaoPesId = evolucao.EvolucaoPesId;
        if (evolucoes.Count > 0) await _db.SaveChangesAsync(ct);
        return treinaram;
    }

    public async Task<int> LembrarAsync(CancellationToken ct)
    {
        var agora = AgoraBrasilia;
        var semana = TreinoDaSemana.Semana(agora);
        var lembreteEm = semana.AddDays(((int)TreinoCriterios.DiaDoLembrete + 6) % 7).AddHours(TreinoCriterios.HoraDoLembrete);
        if (agora < lembreteEm) return 0;

        // Times com técnico ou auxiliar.
        var times = await _db.TreinadorPassagens.AsNoTracking()
            .Where(p => p.Ate == null)
            .Select(p => p.TimeId)
            .Distinct()
            .ToListAsync(ct);

        var avisados = 0;
        var utc = _time.GetUtcNow().UtcDateTime;
        foreach (var timeId in times)
        {
            var chave = $"treino:{timeId}:{semana:yyyy-MM-dd}";
            if (await _db.NotificacoesEnviadas.AnyAsync(n => n.Chave == chave, ct)) continue;

            var limites = await LimitesAsync(timeId, ct);
            var elenco = await ElencoAsync(timeId, limites, ct);
            var podem = elenco.Count(j => j.PorQueNaoTreina is null);
            var escolhidos = await _db.TreinosSemanais.CountAsync(t => t.TeamId == timeId && t.Semana == semana, ct);
            var faltam = Math.Min(limites.Vagas, podem) - escolhidos;
            if (faltam <= 0) continue;

            _db.NotificacoesEnviadas.Add(new NotificacaoEnviada { Chave = chave, EnviadaEm = utc });
            AvisosDoTime.Criar(_db, timeId, AvisosDoTime.Treino,
                escolhidos == 0
                    ? $"🏋️ Você ainda não montou o treino da semana: escolha até {limites.Vagas} jovens antes de domingo à noite."
                    : $"🏋️ Ainda cabe{(faltam > 1 ? "m" : "")} {faltam} jogador{(faltam > 1 ? "es" : "")} no treino da semana: escolha antes de domingo à noite.",
                $"/teams/details/{timeId}?aba=treino", utc);
            avisados++;
        }

        if (avisados > 0) await _db.SaveChangesAsync(ct);
        return avisados;
    }

    public async Task<IReadOnlyList<TreinoResultadoDto>> DoJogadorAsync(int playerId, CancellationToken ct)
    {
        var inicio = await InicioDaTemporadaAsync(ct);
        return await _db.TreinosSemanais.AsNoTracking()
            .Where(t => t.PlayerId == playerId && t.AplicadoEm != null && t.AplicadoEm >= inicio)
            .OrderBy(t => t.Semana)
            .Select(t => new TreinoResultadoDto(t.Semana, t.PlayerId, t.Player.Name, t.Foco, t.Atributo,
                t.OverallAntes, t.OverallDepois, t.SemEfeito, t.AplicadoEm, t.AtributoDoPadrinho))
            .ToListAsync(ct);
    }

    public async Task ApadrinharAsync(Guid teamId, int padrinhoId, int afilhadoId, CancellationToken ct)
    {
        var temporada = await TemporadaAtualAsync(ct)
            ?? throw new InvalidOperationException("Não há temporada em andamento.");
        if (await ParAtivoAsync(teamId, temporada, ct) is not null)
            throw new InvalidOperationException("O time já tem padrinho nesta temporada: o par vale até um dos dois sair do clube.");

        var elenco = (await ElencoAsync(teamId, await LimitesAsync(teamId, ct), ct)).ToDictionary(j => j.PlayerId);
        if (!elenco.TryGetValue(padrinhoId, out var padrinho) || !elenco.TryGetValue(afilhadoId, out var afilhado))
            throw new InvalidOperationException("Padrinho e afilhado precisam estar no seu elenco.");
        if (padrinho.Emprestado)
            throw new InvalidOperationException($"{padrinho.Nome} está emprestado: não pode ser padrinho.");
        if (!(padrinho.Idade >= PadrinhoCriterios.IdadeMinima))
            throw new InvalidOperationException($"Padrinho precisa ter pelo menos {PadrinhoCriterios.IdadeMinima} anos.");
        if (afilhado.PorQueNaoTreina is { } motivo)
            throw new InvalidOperationException($"{afilhado.Nome} não pode ser afilhado: {motivo}.");
        if (padrinho.Grupo != afilhado.Grupo)
            throw new InvalidOperationException("Padrinho e afilhado precisam ser da mesma posição.");

        _db.Apadrinhamentos.Add(new Apadrinhamento
        {
            ApadrinhamentoId = Guid.NewGuid(), TeamId = teamId, Temporada = temporada,
            PadrinhoId = padrinhoId, AfilhadoId = afilhadoId, CriadoEm = _time.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PadrinhoDto?> PadrinhoDoJogadorAsync(int playerId, CancellationToken ct)
    {
        var timeId = await _db.TeamRosters.AsNoTracking().Where(r => r.PlayerId == playerId).Select(r => (Guid?)r.TeamId).FirstOrDefaultAsync(ct);
        if (timeId is not Guid time) return null;
        var par = await ParAtivoAsync(time, await TemporadaAtualAsync(ct), ct);
        return par is not null && (par.PadrinhoId == playerId || par.AfilhadoId == playerId) ? await ParaDtoAsync(par, ct) : null;
    }

    /// <summary>
    /// Padrinhos que ganham o bônus na evolução da virada de <paramref name="temporada"/>: seguem no clube do par e o
    /// afilhado treinou com eles pelo menos <see cref="PadrinhoCriterios.TreinosParaOBonus"/> semanas. Padrinho → afilhado.
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, string>> PadrinhosComBonusAsync(DraftDbContext db, int temporada, CancellationToken ct)
    {
        var pares = await db.Apadrinhamentos.AsNoTracking()
            .Where(p => p.Temporada == temporada
                        && db.TeamRosters.Any(r => r.TeamId == p.TeamId && r.PlayerId == p.PadrinhoId))
            .Select(p => new
            {
                p.PadrinhoId, Afilhado = p.Afilhado.Name,
                Treinos = db.TreinosSemanais.Count(t => t.TeamId == p.TeamId && t.PlayerId == p.AfilhadoId
                                                         && t.AtributoDoPadrinho != null && t.AplicadoEm >= p.CriadoEm)
            })
            .ToListAsync(ct);
        return pares.Where(p => p.Treinos >= PadrinhoCriterios.TreinosParaOBonus)
            .GroupBy(p => p.PadrinhoId)
            .ToDictionary(g => g.Key, g => g.First().Afilhado);
    }

    public async Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct)
    {
        var formados = await _db.TreinosSemanais.AsNoTracking()
            .Where(t => t.OverallAntes <= TreinoCriterios.OverallMaximo && t.OverallDepois > TreinoCriterios.OverallMaximo)
            .Select(t => new { t.AplicadoEm, t.PlayerId, t.Player.Name, t.Team.TeamName, t.OverallDepois, t.Foco })
            .ToListAsync(ct);

        return formados
            .Select(f => new PlantaoNoticiaDto(f.AplicadoEm!.Value, PlantaoCategoria.Carreira, "🎓",
                $"{f.Name} se forma no treino do {f.TeamName} e chega a {f.OverallDepois}",
                $"Passou de {TreinoCriterios.OverallMaximo} treinando {TreinoDaSemana.Nome(f.Foco).ToLowerInvariant()}: agora está pronto e sai do treino",
                $"/players/details/{f.PlayerId}", f.TeamName, f.PlayerId))
            .ToList();
    }

    // ── Bastidores ─────────────────────────────────────────────────────────────

    private sealed record JogadorDoElenco(int PlayerId, string Nome, string Posicao, int Overall, int? Idade, bool Goleiro,
        string? PorQueNaoTreina, GrupoDePosicao Grupo, bool Emprestado);

    /// <summary>Vagas e idade do treino do time (o treinador da base aumenta) e se ele tem treinador de goleiros.</summary>
    private sealed record Limites(int Vagas, int IdadeMaxima, bool TreinadorDeGoleiros);

    private async Task<Limites> LimitesAsync(Guid teamId, CancellationToken ct)
    {
        var cargos = await ComissaoService.CargosAsync(_db, teamId, await TemporadaAtualAsync(ct), ct);
        var base_ = cargos.Contains(CargoDaComissao.TreinadorDaBase);
        return new Limites(
            base_ ? ComissaoCriterios.VagasComTreinadorDaBase : TreinoCriterios.JogadoresPorSemana,
            base_ ? ComissaoCriterios.IdadeComTreinadorDaBase : TreinoCriterios.IdadeMaxima,
            cargos.Contains(CargoDaComissao.TreinadorDeGoleiros));
    }

    private async Task<List<JogadorDoElenco>> ElencoAsync(Guid teamId, Limites limites, CancellationToken ct)
    {
        var emprestados = await _db.Emprestimos.AsNoTracking()
            .Where(e => e.Status == EmprestimoStatus.Ativo && e.TomadorTeamId == teamId)
            .Select(e => e.PlayerId)
            .ToListAsync(ct);

        return (await _db.TeamRosters.AsNoTracking()
                .Where(r => r.TeamId == teamId)
                .Select(r => new
                {
                    r.PlayerId, r.Player.Name, Posicao = r.Player.Position.Name, r.Player.PositionId, r.Player.Overall,
                    r.Player.Age, TemAtributos = r.Player.Atributos != null && r.Player.Atributos.PosicaoPes != null
                })
                .ToListAsync(ct))
            .Select(r => new JogadorDoElenco(r.PlayerId, r.Name, r.Posicao, r.Overall, r.Age,
                r.PositionId == (short)PositionType.Goleiro,
                TreinoDaSemana.PorQueNaoTreina(r.Age, r.Overall, r.TemAtributos, emprestados.Contains(r.PlayerId), limites.IdadeMaxima),
                PapelDoJogador.Grupo(r.PositionId), emprestados.Contains(r.PlayerId)))
            .ToList();
    }

    /// <summary>O par do time na temporada que ainda vale (os dois no clube); nulo se não tem.</summary>
    private async Task<Apadrinhamento?> ParAtivoAsync(Guid teamId, int? temporada, CancellationToken ct) =>
        temporada is null
            ? null
            : await _db.Apadrinhamentos.AsNoTracking()
                .Where(p => p.TeamId == teamId && p.Temporada == temporada
                            && _db.TeamRosters.Any(r => r.TeamId == teamId && r.PlayerId == p.PadrinhoId)
                            && _db.TeamRosters.Any(r => r.TeamId == teamId && r.PlayerId == p.AfilhadoId))
                .OrderByDescending(p => p.CriadoEm)
                .FirstOrDefaultAsync(ct);

    private async Task<PadrinhoDto> ParaDtoAsync(Apadrinhamento par, CancellationToken ct)
    {
        var nomes = await _db.Players.AsNoTracking()
            .Where(p => p.PlayerId == par.PadrinhoId || p.PlayerId == par.AfilhadoId)
            .ToDictionaryAsync(p => p.PlayerId, p => p.Name, ct);
        var treinos = await _db.TreinosSemanais.CountAsync(
            t => t.TeamId == par.TeamId && t.PlayerId == par.AfilhadoId && t.AtributoDoPadrinho != null && t.AplicadoEm >= par.CriadoEm, ct);
        return new PadrinhoDto(par.PadrinhoId, nomes[par.PadrinhoId], par.AfilhadoId, nomes[par.AfilhadoId], treinos);
    }

    /// <summary>Temporada em andamento: a da Série A mais recente que já começou.</summary>
    private async Task<int?> TemporadaAtualAsync(CancellationToken ct)
    {
        var hoje = AgoraBrasilia;
        return await _db.Ligas.AsNoTracking()
            .Where(l => l.Tipo == TipoCompetition.Liga && l.DataInicio <= hoje)
            .OrderByDescending(l => l.DataInicio)
            .Select(l => l.Temporada)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Começo da temporada atual: o da Série A mais recente que já começou.</summary>
    private async Task<DateTime> InicioDaTemporadaAsync(CancellationToken ct)
    {
        var hoje = AgoraBrasilia;
        return await _db.Ligas.AsNoTracking()
            .Where(l => l.Tipo == TipoCompetition.Liga && l.DataInicio <= hoje)
            .OrderByDescending(l => l.DataInicio)
            .Select(l => (DateTime?)l.DataInicio)
            .FirstOrDefaultAsync(ct) ?? DateTime.MinValue;
    }
}
