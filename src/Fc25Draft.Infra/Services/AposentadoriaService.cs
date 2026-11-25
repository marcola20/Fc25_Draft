using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Aposentadoria com aviso. O anúncio grava <see cref="Player.UltimaTemporada"/>; na virada seguinte o jogador sai
/// do elenco (a escalação se limpa sozinha ao salvar) e fica marcado em <see cref="Player.AposentadoNaTemporada"/>.
/// </summary>
public class AposentadoriaService : IAposentadoriaService
{
    private static readonly System.Globalization.CultureInfo Br = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public AposentadoriaService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<AposentadoriaPainelDto> GetPainelAsync(int temporada, CancellationToken ct)
    {
        // A idade que vale é a da temporada: se a próxima já fez aniversário, desconta.
        var aniversariosDepois = await _db.EnvelhecimentosTemporada.AsNoTracking().CountAsync(e => e.Temporada > temporada, ct);

        var jogadores = (await _db.Players.AsNoTracking()
                .Where(p => p.Age != null || p.AposentadoNaTemporada != null)
                .Select(p => new
                {
                    p.PlayerId, p.Name, p.Age, p.Overall, p.PositionId, Posicao = p.Position.Name,
                    PosicaoPes = p.Atributos != null ? p.Atributos.PosicaoPes : null,
                    TimeNome = p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault(),
                    p.UltimaTemporada, p.AposentadoNaTemporada, Revelado = p.DespedidaAnunciadaEm != null
                })
                .ToListAsync(ct))
            .Select(p =>
            {
                var idade = (p.Age ?? 0) - aniversariosDepois;
                var goleiro = p.PosicaoPes is int pp ? pp == 0 : p.PositionId == (short)PositionType.Goleiro;
                var chance = p.AposentadoNaTemporada is null ? Aposentadoria.Chance(idade, goleiro, p.Overall) : 0;
                return new AposentadoriaJogadorDto(p.PlayerId, p.Name, p.TimeNome, p.Posicao, idade, p.Overall, Math.Round(chance, 3),
                    Aposentadoria.Sorteado(temporada, p.PlayerId, chance), p.UltimaTemporada, p.AposentadoNaTemporada, p.Revelado);
            })
            .ToList();

        var ativos = jogadores.Where(j => j.AposentadoNaTemporada is null).ToList();

        return new AposentadoriaPainelDto(
            temporada,
            ativos.Where(j => j.UltimaTemporada < temporada).OrderByDescending(j => j.Idade).ToArray(),
            ativos.Where(j => j.UltimaTemporada == temporada && j.Revelado).OrderByDescending(j => j.Idade).ToArray(),
            ativos.Where(j => j.UltimaTemporada == temporada && !j.Revelado).OrderByDescending(j => j.Idade).ToArray(),
            // Todos os ativos que ainda não anunciaram: o sorteio sugere, mas o admin pode escolher qualquer um.
            ativos.Where(j => j.UltimaTemporada is null)
                .OrderByDescending(j => j.Sorteado).ThenByDescending(j => j.Chance).ThenByDescending(j => j.Idade).ToArray(),
            jogadores.Where(j => j.AposentadoNaTemporada is not null)
                .OrderByDescending(j => j.AposentadoNaTemporada).ThenBy(j => j.Nome).ToArray());
    }

    public async Task AnunciarAsync(int temporada, IReadOnlyCollection<int> playerIds, CancellationToken ct, bool emSegredo = false)
    {
        var ids = playerIds.Distinct().ToList();
        var jogadores = await _db.Players.Where(p => ids.Contains(p.PlayerId)).ToListAsync(ct);

        var aposentados = jogadores.Where(p => p.AposentadoNaTemporada is not null).Select(p => p.Name).ToList();
        if (aposentados.Count > 0)
            throw new InvalidOperationException($"Já aposentados: {string.Join(", ", aposentados)}.");

        var novos = jogadores.Where(p => p.UltimaTemporada != temporada).ToList();
        foreach (var p in novos)
        {
            p.UltimaTemporada = temporada;
            p.DespedidaAnunciadaEm = null;
        }

        if (!emSegredo)
            await AnunciarAoPublicoAsync(novos, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevelarAsync(IReadOnlyCollection<int> playerIds, CancellationToken ct)
    {
        var ids = playerIds.Distinct().ToList();
        var guardados = await _db.Players
            .Where(p => ids.Contains(p.PlayerId) && p.UltimaTemporada != null && p.DespedidaAnunciadaEm == null && p.AposentadoNaTemporada == null)
            .ToListAsync(ct);
        if (guardados.Count == 0)
            throw new InvalidOperationException("Nenhuma despedida guardada para soltar.");

        await AnunciarAoPublicoAsync(guardados, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>A despedida vira pública: data do anúncio (notícia do Plantão) e aviso para o clube (sino e celular).</summary>
    private async Task AnunciarAoPublicoAsync(IReadOnlyList<Player> jogadores, CancellationToken ct)
    {
        var agora = _time.GetUtcNow().UtcDateTime;
        foreach (var p in jogadores)
            p.DespedidaAnunciadaEm = agora;

        var ids = jogadores.Select(p => p.PlayerId).ToList();
        var times = await _db.TeamRosters.AsNoTracking().Where(r => ids.Contains(r.PlayerId)).ToListAsync(ct);
        foreach (var r in times)
        {
            var p = jogadores.First(x => x.PlayerId == r.PlayerId);
            AvisosDoTime.Criar(_db, r.TeamId, AvisosDoTime.Carreira,
                $"👋 {p.Name} anunciou que {p.UltimaTemporada} é a última temporada dele: se aposenta na virada.",
                $"/players/details/{r.PlayerId}", agora);
        }
    }

    public async Task CancelarAnuncioAsync(int playerId, CancellationToken ct)
    {
        var p = await _db.Players.FirstOrDefaultAsync(x => x.PlayerId == playerId, ct)
            ?? throw new InvalidOperationException("Jogador não encontrado.");
        if (p.AposentadoNaTemporada is not null)
            throw new InvalidOperationException($"{p.Name} já se aposentou: use desfazer a aposentadoria.");

        p.UltimaTemporada = null;
        p.DespedidaAnunciadaEm = null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> AposentarAsync(int temporada, CancellationToken ct)
    {
        var quantos = 0;
        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transacao = await _db.Database.BeginTransactionAsync(ct);

            var jogadores = await _db.Players
                .Where(p => p.AposentadoNaTemporada == null && p.UltimaTemporada != null && p.UltimaTemporada < temporada)
                .ToListAsync(ct);
            var ids = jogadores.Select(p => p.PlayerId).ToList();
            var vinculos = await _db.TeamRosters.Where(r => ids.Contains(r.PlayerId)).ToListAsync(ct);
            var emprestimos = await _db.Emprestimos
                .Where(e => ids.Contains(e.PlayerId) && e.Status == EmprestimoStatus.Ativo)
                .ToListAsync(ct);
            var agora = _time.GetUtcNow().UtcDateTime;

            // O clube recebe o que a venda rápida daria (mesmo preço, mesmos 80%). Emprestado: quem recebe é o dono.
            var precos = new PricingService(_db, new ParticipacaoService(_db, _time));
            var timesQueRecebem = vinculos.Select(r => r.TeamId).Concat(emprestimos.Select(e => e.DonoTeamId)).Distinct().ToList();
            var times = await _db.Teams.Where(t => timesQueRecebem.Contains(t.TeamId)).ToDictionaryAsync(t => t.TeamId, ct);
            // Ídolo que se aposenta vira lenda do clube (e, se se aposentou nele, a camisa fica aposentada).
            var idolos = await _db.Idolos.AsNoTracking()
                .Where(i => ids.Contains(i.JogadorId))
                .Select(i => new { i.JogadorId, i.TimeId, i.Time.TeamName })
                .ToListAsync(ct);

            foreach (var p in jogadores)
            {
                var vinculo = vinculos.FirstOrDefault(r => r.PlayerId == p.PlayerId);
                var quemRecebe = emprestimos.FirstOrDefault(e => e.PlayerId == p.PlayerId)?.DonoTeamId ?? vinculo?.TeamId;
                decimal? pagamento = null;
                if (quemRecebe is Guid recebedor && times.TryGetValue(recebedor, out var clube))
                {
                    var preco = (await precos.CalculateForPlayerAsync(p.PlayerId, ct)).BasePrice;
                    if (preco > 0m)
                    {
                        pagamento = QuickSellOverallCalculator.Pagamento(preco);
                        clube.Budget = decimal.Round(clube.Budget + pagamento.Value, 2, MidpointRounding.AwayFromZero);
                        ExtratoCaixa.Lancar(_db, clube.TeamId, pagamento.Value, ExtratoCaixa.Aposentadoria,
                            $"Aposentadoria de {p.Name}", agora);
                    }
                }

                p.TimeAoSeAposentar = vinculo?.TeamId ?? p.CurrentTeamId;
                p.NumeroAoSeAposentar = vinculo?.NumeroCamisa;
                p.AposentadoNaTemporada = p.UltimaTemporada;
                p.AposentadoEm = agora;
                p.CurrentTeamId = null;

                // Sai do elenco: a escalação se limpa sozinha no SaveChanges (EscalacoesDeQuemSaiu).
                foreach (var r in vinculos.Where(r => r.PlayerId == p.PlayerId))
                    _db.TeamRosters.Remove(r);

                var valorTexto = pagamento is decimal v ? v.ToString("C", Br) : null;
                if (p.TimeAoSeAposentar is Guid time)
                    AvisosDoTime.Criar(_db, time, AvisosDoTime.Carreira,
                        $"🎖️ {p.Name} pendurou as chuteiras e saiu do elenco." +
                        (valorTexto is not null && quemRecebe == time ? $" O clube recebeu {valorTexto}." : ""),
                        $"/players/details/{p.PlayerId}", agora);
                if (valorTexto is not null && quemRecebe is Guid dono && dono != p.TimeAoSeAposentar)
                    AvisosDoTime.Criar(_db, dono, AvisosDoTime.Carreira,
                        $"🎖️ {p.Name}, emprestado, pendurou as chuteiras. O clube recebeu {valorTexto}.",
                        $"/players/details/{p.PlayerId}", agora);

                foreach (var idolo in idolos.Where(i => i.JogadorId == p.PlayerId))
                {
                    var camisa = idolo.TimeId == p.TimeAoSeAposentar && p.NumeroAoSeAposentar is short n
                        ? $" A camisa {n} está aposentada: ninguém mais usa no clube."
                        : "";
                    AvisosDoTime.Criar(_db, idolo.TimeId, AvisosDoTime.Idolo,
                        $"👑 {p.Name} pendurou as chuteiras e virou lenda do {idolo.TeamName}.{camisa}",
                        "/idolos", agora);
                }

                _db.TransferHistories.Add(new TransferHistory
                {
                    TransferId = Guid.NewGuid(),
                    Type = TransferType.Aposentadoria,
                    PlayerId = p.PlayerId,
                    FromTeamId = p.TimeAoSeAposentar,
                    ToTeamId = null,
                    Amount = pagamento,
                    Notes = $"{p.Name} se aposentou depois da temporada {p.AposentadoNaTemporada}." +
                            (valorTexto is not null ? $" O clube recebeu {valorTexto} (valor da venda rápida)." : ""),
                    PerformedBy = "Aposentadoria",
                    PerformedAtUtc = agora,
                    OldOverall = p.Overall,
                    NewOverall = p.Overall
                });
            }

            await _db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
            quantos = jogadores.Count;
        });
        return quantos;
    }

    public async Task DesaposentarAsync(int playerId, CancellationToken ct)
    {
        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transacao = await _db.Database.BeginTransactionAsync(ct);

            var p = await _db.Players.FirstOrDefaultAsync(x => x.PlayerId == playerId, ct)
                ?? throw new InvalidOperationException("Jogador não encontrado.");
            if (p.AposentadoNaTemporada is null)
                throw new InvalidOperationException($"{p.Name} não está aposentado.");

            // Volta para o clube, se ele ainda existir e o jogador não estiver em outro elenco.
            if (p.TimeAoSeAposentar is Guid time
                && await _db.Teams.AnyAsync(t => t.TeamId == time, ct)
                && !await _db.TeamRosters.AnyAsync(r => r.PlayerId == p.PlayerId, ct))
            {
                // O número volta com ele, se ninguém pegou nesse meio tempo.
                var numero = p.NumeroAoSeAposentar is short n
                             && !await _db.TeamRosters.AnyAsync(r => r.TeamId == time && r.NumeroCamisa == n, ct)
                    ? p.NumeroAoSeAposentar
                    : null;
                _db.TeamRosters.Add(new TeamRoster { TeamId = time, PlayerId = p.PlayerId, NumeroCamisa = numero });
                p.CurrentTeamId = time;
            }

            var historico = await _db.TransferHistories
                .Where(t => t.PlayerId == p.PlayerId && t.Type == TransferType.Aposentadoria)
                .ToListAsync(ct);
            _db.TransferHistories.RemoveRange(historico);

            // Devolve o que o clube recebeu na aposentadoria (o lançamento é do mesmo instante).
            if (p.AposentadoEm is DateTime quando)
            {
                var descricao = $"Aposentadoria de {p.Name}";
                var pagos = await _db.BudgetLedgers
                    .Where(l => l.Origem == ExtratoCaixa.Aposentadoria && l.Tipo == "CREDIT" && l.Descricao == descricao && l.DataUtc == quando)
                    .ToListAsync(ct);
                var agora = _time.GetUtcNow().UtcDateTime;
                foreach (var pago in pagos)
                {
                    var clube = await _db.Teams.FirstOrDefaultAsync(t => t.TeamId == pago.TeamId, ct);
                    if (clube is null) continue;
                    clube.Budget = decimal.Round(clube.Budget - pago.Valor, 2, MidpointRounding.AwayFromZero);
                    ExtratoCaixa.Lancar(_db, clube.TeamId, -pago.Valor, ExtratoCaixa.Aposentadoria,
                        $"{p.Name} voltou da aposentadoria (devolução)", agora);
                }
            }

            p.AposentadoNaTemporada = null;
            p.AposentadoEm = null;
            p.UltimaTemporada = null;
            p.DespedidaAnunciadaEm = null;
            p.TimeAoSeAposentar = null;
            p.NumeroAoSeAposentar = null;

            await _db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        });
    }

    public async Task<IReadOnlyList<AposentadoPesDto>> ListAposentadosParaPesAsync(CancellationToken ct) =>
        await _db.Players.AsNoTracking()
            .Where(p => p.AposentadoNaTemporada != null)
            .OrderBy(p => p.Name)
            .Select(p => new AposentadoPesDto(p.PlayerId, p.Name, p.Atributos != null ? p.Atributos.PesId : null, p.AposentadoNaTemporada!.Value))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct)
    {
        var jogadores = await _db.Players.AsNoTracking()
            .Where(p => p.DespedidaAnunciadaEm != null || p.AposentadoEm != null)
            .Select(p => new
            {
                p.PlayerId, p.Name, p.Age, p.UltimaTemporada, p.DespedidaAnunciadaEm, p.AposentadoNaTemporada, p.AposentadoEm,
                TimeAtual = p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault(),
                TimeAoSeAposentar = _db.Teams.Where(t => t.TeamId == p.TimeAoSeAposentar).Select(t => t.TeamName).FirstOrDefault()
            })
            .ToListAsync(ct);

        var noticias = new List<PlantaoNoticiaDto>();
        foreach (var p in jogadores)
        {
            var link = $"/players/details/{p.PlayerId}";
            if (p.DespedidaAnunciadaEm is DateTime anunciado && p.UltimaTemporada is int ultima)
            {
                var time = p.TimeAoSeAposentar ?? p.TimeAtual;
                noticias.Add(new PlantaoNoticiaDto(anunciado, PlantaoCategoria.Carreira, "👋",
                    $"{p.Name} anuncia que {ultima} é sua última temporada",
                    time is null ? "A despedida dos gramados está marcada" : $"A despedida será pelo {time}", link, time, p.PlayerId));
            }
            if (p.AposentadoEm is DateTime aposentado)
                noticias.Add(new PlantaoNoticiaDto(aposentado, PlantaoCategoria.Carreira, "🎖️",
                    $"{p.Name} pendura as chuteiras",
                    p.TimeAoSeAposentar is null ? "Fim de carreira" : $"Encerra a carreira no {p.TimeAoSeAposentar}", link, p.TimeAoSeAposentar, p.PlayerId));
        }

        return noticias.OrderByDescending(n => n.Data).ToArray();
    }
}
