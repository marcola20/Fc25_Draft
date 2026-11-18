using System.Globalization;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Extensions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Premiações individuais de estatística: top 3 de artilharia, assistências e clean sheets da competição. O clube
/// recebe o dinheiro e o jogador o +1 over (pelos atributos, para o Editor PES levar ao save). Quem entra em duas
/// estatísticas da temporada fica com um over; o outro é repassado a um companheiro que o treinador escolhe.
/// </summary>
public class PremiosEstatisticaService : IPremiosEstatisticaService
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Mesma origem da premiação por classificação: no extrato fica em "Premiação".</summary>
    private const string OrigemLedger = "PREMIACAO";

    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public PremiosEstatisticaService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    private DateTime AgoraUtc => _time.GetUtcNow().UtcDateTime;

    // ── Admin ────────────────────────────────────────────────────────────────

    public async Task<PremiosEstatisticaDto> GetAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(l => l.LigaId == ligaId, ct)
                   ?? throw new InvalidOperationException("Competição não encontrada.");
        var encerrada = liga.Status == LigaStatus.Encerrada;

        var dados = await _db.PremiosEstatistica.AsNoTracking()
            .Where(p => p.LigaId == ligaId)
            .Select(p => new
            {
                p.PremioId, p.Categoria, p.Posicao, p.Numero, p.PlayerId, Nome = p.Player.Name, p.TimeId,
                TimeNome = p.Time != null ? p.Time.TeamName : null, p.Valor, p.Repasse, p.OverParaPlayerId,
                OverParaNome = p.OverPara != null ? p.OverPara.Name : null, p.OverAplicadoEm, p.PagoEm,
                NoSave = p.Evolucao != null && p.Evolucao.AplicadaNoJogoEmUtc != null
            })
            .ToListAsync(ct);

        if (dados.Count > 0)
            return new PremiosEstatisticaDto(liga.LigaId, liga.Nome, liga.Temporada, encerrada, true, dados.Max(d => d.PagoEm), null,
                dados.OrderBy(d => d.Categoria).ThenBy(d => d.Posicao).ThenBy(d => d.Nome, StringComparer.Create(Br, true))
                    .Select(d => new PremioEstatisticaLinhaDto(d.PremioId, d.Categoria, d.Posicao, d.Numero, d.PlayerId, d.Nome,
                        d.TimeId, d.TimeNome, d.Valor, d.Repasse, d.OverParaPlayerId, d.OverParaNome, d.OverAplicadoEm is not null, d.NoSave))
                    .ToList());

        var linhas = await CalcularAsync(liga, ct);
        string? impedimento = liga.Temporada is null ? "A competição não tem temporada definida."
            : !encerrada ? "A competição ainda não foi encerrada: os números podem mudar."
            : linhas.Count == 0 ? "Ninguém fez gol, assistência ou clean sheet nessa competição."
            : null;

        return new PremiosEstatisticaDto(liga.LigaId, liga.Nome, liga.Temporada, encerrada, false, null, impedimento, linhas);
    }

    public async Task<string> DarAsync(Guid ligaId, CancellationToken ct)
    {
        var previa = await GetAsync(ligaId, ct);
        if (previa.JaDado) throw new InvalidOperationException("Os prêmios de estatística dessa competição já foram dados.");
        if (previa.Impedimento is not null) throw new InvalidOperationException(previa.Impedimento);

        var temporada = previa.Temporada!.Value;
        var agora = AgoraUtc;

        var playerIds = previa.Linhas.Select(l => l.PlayerId).Distinct().ToList();
        var jogadores = await _db.Players.Include(p => p.Atributos)
            .Where(p => playerIds.Contains(p.PlayerId))
            .ToDictionaryAsync(p => p.PlayerId, ct);
        var timeIds = previa.Linhas.Where(l => l.TimeId is not null).Select(l => l.TimeId!.Value).Distinct().ToList();
        var times = await _db.Teams.Where(t => timeIds.Contains(t.TeamId)).ToDictionaryAsync(t => t.TeamId, ct);

        var overs = new List<string>();
        var faltando = new List<string>();
        var repasses = new List<string>();

        foreach (var linha in previa.Linhas)
        {
            var jogador = jogadores[linha.PlayerId];
            var titulo = $"{PremiosEstatistica.Titulo(linha.Categoria, linha.Posicao)} ({previa.LigaNome})";

            var premio = new PremioEstatistica
            {
                PremioId = Guid.NewGuid(),
                LigaId = ligaId,
                Temporada = temporada,
                Categoria = linha.Categoria,
                Posicao = linha.Posicao,
                Numero = linha.Numero,
                PlayerId = linha.PlayerId,
                TimeId = linha.TimeId,
                Valor = linha.Valor,
                PagoEm = HorarioDeBrasilia.Agora(_time),
                Repasse = linha.Repasse,
            };
            _db.PremiosEstatistica.Add(premio);

            if (linha.TimeId is Guid timeId && times.TryGetValue(timeId, out var time) && linha.Valor > 0)
            {
                time.Budget = decimal.Round(time.Budget + linha.Valor, 2, MidpointRounding.AwayFromZero);
                _db.BudgetLedgers.Add(new BudgetLedger
                {
                    BudgetLedgerId = Guid.NewGuid(),
                    TeamId = timeId,
                    DataUtc = agora,
                    Tipo = "CREDIT",
                    Origem = OrigemLedger,
                    Valor = linha.Valor,
                    Descricao = $"{jogador.Name}, {titulo}"
                });
            }

            if (linha.Repasse)
            {
                repasses.Add($"{jogador.Name} ({PremiosEstatistica.Titulo(linha.Categoria, linha.Posicao)})");
                if (jogador.CurrentTeamId is Guid clube)
                    AvisosDoTime.Criar(_db, clube, AvisosDoTime.Premio,
                        $"{jogador.Name} foi {titulo} (+{Mi(linha.Valor)}), mas já levou over por outra estatística: escolha na Minha Área o companheiro que recebe o +1 over.",
                        "/minha-area", agora);
                continue;
            }

            try
            {
                var (registro, resumo) = OverDePremio.Dar(_db, jogador, $"Prêmio {titulo}", agora);
                premio.OverParaPlayerId = jogador.PlayerId;
                premio.OverAplicadoEm = HorarioDeBrasilia.Agora(_time);
                premio.Evolucao = registro;
                overs.Add(resumo);

                if (linha.TimeId is Guid clube)
                    AvisosDoTime.Criar(_db, clube, AvisosDoTime.Premio,
                        $"{jogador.Name} foi {titulo}: +{Mi(linha.Valor)} no caixa e +1 over (overall {jogador.Overall}).",
                        $"/players/details/{jogador.PlayerId}", agora);
            }
            catch (InvalidOperationException ex)
            {
                faltando.Add(ex.Message);
            }
        }

        await _db.SaveChangesAsync(ct);

        var total = previa.Linhas.Sum(l => l.Valor);
        var texto = $"Prêmios dados: {Mi(total)} para os clubes, +1 over em {overs.Count} jogador{(overs.Count == 1 ? "" : "es")}.";
        if (overs.Count > 0) texto += " Abra o Editor PES para levar ao save.";
        if (repasses.Count > 0) texto += $" Repasse esperando o treinador escolher: {string.Join(", ", repasses)}.";
        if (faltando.Count > 0) texto += $" Over não dado: {string.Join("; ", faltando)}";
        return texto;
    }

    public async Task<string> DesfazerAsync(Guid ligaId, CancellationToken ct)
    {
        var premios = await _db.PremiosEstatistica.Include(p => p.Evolucao).Include(p => p.Liga)
            .Where(p => p.LigaId == ligaId)
            .ToListAsync(ct);
        if (premios.Count == 0) throw new InvalidOperationException("Essa competição não tem prêmios de estatística dados.");

        var comOver = premios.Where(p => p.OverAplicadoEm is not null && p.OverParaPlayerId is not null).ToList();
        var noSave = comOver.Where(p => p.Evolucao?.AplicadaNoJogoEmUtc is not null).Select(p => p.OverParaPlayerId!.Value).ToList();
        if (noSave.Count > 0)
        {
            var nomes = await _db.Players.AsNoTracking().Where(p => noSave.Contains(p.PlayerId)).Select(p => p.Name).ToListAsync(ct);
            throw new InvalidOperationException($"O Editor PES já levou o over de {string.Join(", ", nomes)} para o save: desfaça no jogo e ajuste na ficha antes.");
        }

        var alvos = comOver.Select(p => p.OverParaPlayerId!.Value).Distinct().ToList();
        var jogadores = await _db.Players.Include(p => p.Atributos).Where(p => alvos.Contains(p.PlayerId)).ToDictionaryAsync(p => p.PlayerId, ct);
        foreach (var p in comOver)
            OverDePremio.Desfazer(_db, jogadores[p.OverParaPlayerId!.Value], p.Evolucao);

        var timeIds = premios.Where(p => p.TimeId is not null).Select(p => p.TimeId!.Value).Distinct().ToList();
        var times = await _db.Teams.Where(t => timeIds.Contains(t.TeamId)).ToDictionaryAsync(t => t.TeamId, ct);
        var agora = AgoraUtc;
        foreach (var p in premios.Where(p => p.TimeId is not null && p.Valor > 0))
        {
            if (!times.TryGetValue(p.TimeId!.Value, out var time)) continue;
            time.Budget = decimal.Round(time.Budget - p.Valor, 2, MidpointRounding.AwayFromZero);
            _db.BudgetLedgers.Add(new BudgetLedger
            {
                BudgetLedgerId = Guid.NewGuid(),
                TeamId = time.TeamId,
                DataUtc = agora,
                Tipo = "DEBIT",
                Origem = OrigemLedger,
                Valor = p.Valor,
                Descricao = $"Estorno — {PremiosEstatistica.Titulo(p.Categoria, p.Posicao)} ({p.Liga.Nome})"
            });
        }

        _db.PremiosEstatistica.RemoveRange(premios);
        await _db.SaveChangesAsync(ct);

        return $"Desfeito: {Mi(premios.Sum(p => p.Valor))} tirados dos clubes e {comOver.Count} over{(comOver.Count == 1 ? "" : "s")} desfeito{(comOver.Count == 1 ? "" : "s")}.";
    }

    public async Task<IReadOnlyList<PremioJogadorOpcaoDto>> CompanheirosAsync(Guid premioId, CancellationToken ct)
    {
        var premio = await _db.PremiosEstatistica.AsNoTracking()
            .Where(p => p.PremioId == premioId)
            .Select(p => new { p.PlayerId, Clube = p.Player.CurrentTeamId ?? p.TimeId })
            .FirstOrDefaultAsync(ct);

        return premio?.Clube is Guid clube ? await CompanheirosDoClubeAsync(clube, premio.PlayerId, ct) : Array.Empty<PremioJogadorOpcaoDto>();
    }

    public async Task<string> DarOverAsync(Guid premioId, int? paraPlayerId, CancellationToken ct)
    {
        var premio = await CarregarParaDarAsync(premioId, ct);

        if (premio.Repasse)
        {
            if (paraPlayerId is not int para) throw new InvalidOperationException("Escolha o companheiro que recebe o over.");
            await ExigirCompanheiroAsync(premio, para, ct);
        }
        else if (paraPlayerId is int para && para != premio.PlayerId)
        {
            throw new InvalidOperationException("Esse over é do próprio premiado (não é repasse).");
        }

        return await AplicarAsync(premio, paraPlayerId ?? premio.PlayerId, ct);
    }

    // ── Treinador ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<RepasseDeOverDto>> RepassesPendentesAsync(Guid teamId, CancellationToken ct)
    {
        var pendentes = await _db.PremiosEstatistica.AsNoTracking()
            .Where(p => p.Repasse && p.OverAplicadoEm == null && p.Player.CurrentTeamId == teamId)
            .Select(p => new { p.PremioId, p.PlayerId, Nome = p.Player.Name, p.Categoria, p.Posicao, p.Temporada, Liga = p.Liga.Nome })
            .ToListAsync(ct);
        if (pendentes.Count == 0) return Array.Empty<RepasseDeOverDto>();

        // O over que ele já levou na temporada, para o treinador entender o porquê do repasse.
        var ids = pendentes.Select(p => p.PlayerId).ToList();
        var temporadas = pendentes.Select(p => p.Temporada).Distinct().ToList();
        var levados = await _db.PremiosEstatistica.AsNoTracking()
            .Where(p => !p.Repasse && ids.Contains(p.PlayerId) && temporadas.Contains(p.Temporada))
            .Select(p => new { p.PlayerId, p.Temporada, p.Categoria, p.Posicao, Liga = p.Liga.Nome })
            .ToListAsync(ct);

        var companheiros = new Dictionary<int, IReadOnlyList<PremioJogadorOpcaoDto>>();
        foreach (var id in ids.Distinct())
            companheiros[id] = await CompanheirosDoClubeAsync(teamId, id, ct);

        return pendentes
            .OrderBy(p => p.Nome)
            .Select(p =>
            {
                var levou = levados.Where(l => l.PlayerId == p.PlayerId && l.Temporada == p.Temporada)
                    .OrderBy(l => l.Categoria)
                    .Select(l => $"{PremiosEstatistica.Titulo(l.Categoria, l.Posicao)} ({l.Liga})");
                return new RepasseDeOverDto(p.PremioId, p.PlayerId, p.Nome,
                    $"{PremiosEstatistica.Titulo(p.Categoria, p.Posicao)} ({p.Liga})",
                    string.Join(" e ", levou),
                    companheiros[p.PlayerId]);
            })
            .ToList();
    }

    public async Task<string> RepassarPeloTreinadorAsync(string? token, Guid premioId, int paraPlayerId, CancellationToken ct)
    {
        var acesso = await _db.AcessoPorTokenAsync(token, ct)
                     ?? throw new InvalidOperationException("Entre com o token do seu time.");

        var premio = await CarregarParaDarAsync(premioId, ct);
        if (!premio.Repasse) throw new InvalidOperationException("Esse over não é de repasse.");

        var clube = await _db.Players.AsNoTracking().Where(p => p.PlayerId == premio.PlayerId).Select(p => p.CurrentTeamId).FirstAsync(ct);
        if (clube != acesso.TimeId) throw new InvalidOperationException("Quem escolhe é o treinador do clube do premiado.");

        await ExigirCompanheiroAsync(premio, paraPlayerId, ct);
        return await AplicarAsync(premio, paraPlayerId, ct);
    }

    // ── Apoio ────────────────────────────────────────────────────────────────

    /// <summary>Top 3 de cada estatística, com o repasse já marcado: o primeiro over do jogador na temporada é dele.</summary>
    private async Task<IReadOnlyList<PremioEstatisticaLinhaDto>> CalcularAsync(Liga liga, CancellationToken ct)
    {
        var stats = new LigaPublicService(_db);
        var rankings = new Dictionary<CategoriaEstatistica, IReadOnlyList<(int JogadorId, string Nome, Guid TimeId, string TimeNome, int Numero)>>
        {
            [CategoriaEstatistica.Artilharia] = (await stats.GetArtilheirosAsync(liga.LigaId, ct))
                .Select(a => (a.JogadorId, a.JogadorNome, a.TimeId, a.TimeNome, a.Gols)).ToList(),
            [CategoriaEstatistica.Assistencias] = (await stats.GetAssistenciasAsync(liga.LigaId, ct))
                .Select(a => (a.JogadorId, a.JogadorNome, a.TimeId, a.TimeNome, a.Assistencias)).ToList(),
            [CategoriaEstatistica.CleanSheets] = (await stats.GetCleanSheetsAsync(liga.LigaId, ct))
                .Select(c => (c.JogadorId, c.JogadorNome, c.TimeId, c.TimeNome, c.CleanSheets)).ToList(),
        };

        // Quem já levou over de estatística em outra competição da temporada.
        var jaLevaram = liga.Temporada is int temporada
            ? (await _db.PremiosEstatistica.AsNoTracking()
                .Where(p => p.Temporada == temporada && !p.Repasse)
                .Select(p => p.PlayerId).ToListAsync(ct)).ToHashSet()
            : new HashSet<int>();

        var linhas = new List<PremioEstatisticaLinhaDto>();
        foreach (var regra in PremiosEstatistica.Todas)
        {
            foreach (var (posicao, j) in PremiosEstatistica.Top3(rankings[regra.Categoria], x => x.Numero)
                         .OrderBy(x => x.Posicao).ThenBy(x => x.Item.Nome, StringComparer.Create(Br, true)))
            {
                var repasse = !jaLevaram.Add(j.JogadorId);
                linhas.Add(new PremioEstatisticaLinhaDto(null, regra.Categoria, posicao, j.Numero, j.JogadorId, j.Nome,
                    j.TimeId == Guid.Empty ? null : j.TimeId, j.TimeNome, regra.ValorDa(posicao), repasse, null, null, false, false));
            }
        }

        return linhas;
    }

    private async Task<PremioEstatistica> CarregarParaDarAsync(Guid premioId, CancellationToken ct)
    {
        var premio = await _db.PremiosEstatistica.Include(p => p.Liga).Include(p => p.Player)
                         .FirstOrDefaultAsync(p => p.PremioId == premioId, ct)
                     ?? throw new InvalidOperationException("Prêmio não encontrado.");
        if (premio.OverAplicadoEm is not null) throw new InvalidOperationException("O over desse prêmio já foi dado.");
        return premio;
    }

    private async Task ExigirCompanheiroAsync(PremioEstatistica premio, int paraPlayerId, CancellationToken ct)
    {
        var clube = premio.Player.CurrentTeamId ?? premio.TimeId;
        if (paraPlayerId == premio.PlayerId) throw new InvalidOperationException($"{premio.Player.Name} já levou over por outra estatística: escolha um companheiro.");
        var ok = clube is Guid c && await _db.Players.AnyAsync(p => p.PlayerId == paraPlayerId && p.CurrentTeamId == c && p.AposentadoNaTemporada == null, ct);
        if (!ok) throw new InvalidOperationException("O over só pode ir para um companheiro de clube do premiado.");
    }

    private async Task<string> AplicarAsync(PremioEstatistica premio, int alvoId, CancellationToken ct)
    {
        var jogador = await _db.Players.Include(p => p.Atributos).FirstOrDefaultAsync(p => p.PlayerId == alvoId, ct)
                      ?? throw new InvalidOperationException("Jogador não encontrado.");

        var titulo = $"{PremiosEstatistica.Titulo(premio.Categoria, premio.Posicao)} ({premio.Liga.Nome})";
        var repasse = alvoId != premio.PlayerId;
        var motivo = repasse
            ? $"Prêmio {PremiosEstatistica.Titulo(premio.Categoria, premio.Posicao)} (repasse de {premio.Player.Name})"
            : $"Prêmio {titulo}";

        var agora = AgoraUtc;
        var (registro, resumo) = OverDePremio.Dar(_db, jogador, motivo, agora);
        premio.OverParaPlayerId = alvoId;
        premio.OverAplicadoEm = HorarioDeBrasilia.Agora(_time);
        premio.Evolucao = registro;

        if (repasse && jogador.CurrentTeamId is Guid clube)
            AvisosDoTime.Criar(_db, clube, AvisosDoTime.Premio,
                $"{jogador.Name} recebeu o +1 over repassado por {premio.Player.Name}, {titulo}: overall {jogador.Overall}.",
                $"/players/details/{jogador.PlayerId}", agora);

        await _db.SaveChangesAsync(ct);
        return registro is null ? resumo : resumo + ". O Editor PES leva para o save.";
    }

    private async Task<IReadOnlyList<PremioJogadorOpcaoDto>> CompanheirosDoClubeAsync(Guid clube, int premiadoId, CancellationToken ct)
    {
        var jogadores = await _db.Players.AsNoTracking()
            .Where(p => p.CurrentTeamId == clube && p.PlayerId != premiadoId && p.AposentadoNaTemporada == null)
            .OrderByDescending(p => p.Overall).ThenBy(p => p.Name)
            .Select(p => new { p.PlayerId, p.Name, p.PositionId, p.Overall, Time = p.CurrentTeam!.TeamName })
            .ToListAsync(ct);

        return jogadores.Select(j => new PremioJogadorOpcaoDto(j.PlayerId, j.Name, ((int)j.PositionId).ToPositionSigla(), j.Overall, j.Time)).ToList();
    }

    private static string Mi(decimal valor) => $"{(valor / 1_000_000m).ToString("0.#", Br)} Mi";
}
