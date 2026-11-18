using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <inheritdoc cref="IArtesService"/>
public class ArtesService : IArtesService
{
    /// <summary>Um pedido entregue tantas vezes sem resposta do agente vira "falhou" (o admin manda refazer).</summary>
    private const int MaximoDeEntregas = 3;

    // Draft: até a 3ª escolha, uma arte cada; da 4ª à 8ª, uma arte com as cinco.
    private const int UltimaIndividual = 3;
    private const int PrimeiraDoGrupo = 4;
    private const int UltimaDoGrupo = 8;

    private static readonly TransferType[] TiposDeContratacao =
        [TransferType.MarketAuction, TransferType.TeamSale, TransferType.TeamTrade, TransferType.Loan, TransferType.LoanPurchase];

    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public ArtesService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    private DateTime AgoraUtc => _time.GetUtcNow().UtcDateTime;

    // ── Para o agente ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PedidoArteDto>> PendentesAsync(CancellationToken ct)
    {
        await ProcurarAsync(ct);

        var pedidos = await _db.PedidosArte
            .Where(p => p.Situacao == SituacaoPedidoArte.Pendente)
            .OrderBy(p => p.CriadoEmUtc)
            .Take(10)
            .ToListAsync(ct);

        var saida = new List<PedidoArteDto>();
        foreach (var pedido in pedidos)
        {
            if (pedido.Tentativas >= MaximoDeEntregas)
            {
                pedido.Situacao = SituacaoPedidoArte.Falhou;
                pedido.Erro ??= $"Entregue {pedido.Tentativas} vezes ao agente sem resposta.";
                continue;
            }

            var dto = pedido.Tipo switch
            {
                TipoPedidoArte.Premio => await MontarPremioAsync(pedido, ct),
                TipoPedidoArte.Contratacao => await MontarContratacaoAsync(pedido, ct),
                TipoPedidoArte.SelecaoRodada => await MontarSelecaoAsync(pedido, ct),
                _ => await MontarDraftAsync(pedido, ct)
            };
            if (dto is null)
            {
                pedido.Situacao = SituacaoPedidoArte.Descartado;
                pedido.Erro = "O prêmio ou a transferência não existe mais.";
                continue;
            }

            pedido.Tentativas++;
            pedido.EntregueEmUtc = AgoraUtc;
            saida.Add(dto);
        }

        await _db.SaveChangesAsync(ct);
        return saida;
    }

    public async Task ConcluirAsync(Guid pedidoId, bool ok, string? erro, CancellationToken ct)
    {
        var pedido = await AcharAsync(pedidoId, ct);
        pedido.Situacao = ok ? SituacaoPedidoArte.Feito : SituacaoPedidoArte.Falhou;
        pedido.Erro = ok ? null : Cortar(erro ?? "O agente não disse o motivo.", 1000);
        pedido.ConcluidoEmUtc = AgoraUtc;
        await _db.SaveChangesAsync(ct);
    }

    // ── Achar o que merece arte ──────────────────────────────────────────────

    public async Task<int> ProcurarAsync(CancellationToken ct)
    {
        var cfg = await _db.ArtesConfig.AsNoTracking().FirstOrDefaultAsync(ct);
        if (cfg is null || !cfg.Ligado) return 0;

        var chaves = (await _db.PedidosArte.AsNoTracking().Select(p => p.Chave).ToListAsync(ct)).ToHashSet();
        var novos = new List<PedidoArte>();

        // Prêmios: os vencedores da última temporada publicada.
        var ultimaPublicada = await _db.VotacoesPremios.AsNoTracking()
            .Where(v => v.Situacao == SituacaoVotacao.Publicada)
            .OrderByDescending(v => v.Temporada)
            .Select(v => (int?)v.Temporada)
            .FirstOrDefaultAsync(ct);
        if (ultimaPublicada is int temporada)
        {
            var vencedores = await _db.PremiosCandidatos.AsNoTracking()
                .Where(c => c.Temporada == temporada && c.Vencedor)
                .Select(c => new
                {
                    c.CandidatoId,
                    c.Categoria,
                    Nome = c.Player != null ? c.Player.Name : c.Treinador != null ? c.Treinador.Nome : "?"
                })
                .ToListAsync(ct);
            var ordem = PremiosTemporada.Todas.Select(r => r.Categoria).ToList();
            foreach (var v in vencedores.OrderBy(v => ordem.IndexOf(v.Categoria)))
            {
                var chave = $"premio:{v.CandidatoId}";
                if (chaves.Contains(chave)) continue;
                novos.Add(Novo(TipoPedidoArte.Premio, chave, v.CandidatoId,
                    $"{PremiosTemporada.De(v.Categoria).Sigla} {temporada} · {v.Nome}"));
            }
        }

        // Contratações: depois de ligado, as que passam do valor ou do over mínimo.
        var desde = cfg.TransferenciasDesdeUtc ?? AgoraUtc;
        var transferencias = await _db.TransferHistories.AsNoTracking()
            .Where(t => t.PerformedAtUtc >= desde && TiposDeContratacao.Contains(t.Type) && t.ToTeamId != null)
            .Select(t => new
            {
                t.TransferId,
                t.Type,
                t.Amount,
                t.PerformedAtUtc,
                Jogador = t.Player.Name,
                Over = t.Player.Overall,
                Para = t.ToTeam != null ? t.ToTeam.TeamName : "?"
            })
            .ToListAsync(ct);
        foreach (var t in transferencias.OrderBy(t => t.PerformedAtUtc))
        {
            if (!ArtesRegras.ContratacaoMereceArte(t.Amount, t.Over, cfg.ValorMinimo, cfg.OverMinimo)) continue;
            var chave = $"transferencia:{t.TransferId}";
            if (chaves.Contains(chave)) continue;
            var valor = t.Amount is decimal a && a > 0 ? $" ({ArtesRegras.Valor(a)})" : "";
            var seta = t.Type == TransferType.Loan ? "⇢" : "→";
            novos.Add(Novo(TipoPedidoArte.Contratacao, chave, t.TransferId, $"{t.Jogador} {seta} {t.Para}{valor} · over {t.Over}"));
        }

        // Draft: as 3 primeiras escolhas, uma arte cada, e as escolhas 4 a 8 juntas quando a 8ª sai.
        var escolhas = await _db.DraftPicks.AsNoTracking()
            .Where(p => p.PlayerId != null && p.PickedAtUtc >= desde && p.OverallPick <= UltimaDoGrupo)
            .Select(p => new { p.DraftId, p.OverallPick, Jogador = p.Player!.Name, Clube = p.Team.TeamName, NomeDraft = p.Draft.Name })
            .ToListAsync(ct);
        var draftIds = escolhas.Select(e => e.DraftId).Distinct().ToList();
        var faixas = await _db.DraftPicks.AsNoTracking()
            .Where(p => draftIds.Contains(p.DraftId) && p.OverallPick >= PrimeiraDoGrupo && p.OverallPick <= UltimaDoGrupo)
            .GroupBy(p => p.DraftId)
            .Select(g => new { DraftId = g.Key, Total = g.Count(), Feitas = g.Count(p => p.PlayerId != null) })
            .ToListAsync(ct);
        foreach (var e in escolhas.Where(e => e.OverallPick <= UltimaIndividual).OrderBy(e => e.OverallPick))
        {
            var chave = $"draft:{e.DraftId}:{e.OverallPick}";
            if (chaves.Contains(chave)) continue;
            novos.Add(Novo(TipoPedidoArte.DraftEscolha, chave, e.DraftId,
                $"{e.NomeDraft} · {e.OverallPick}ª escolha · {e.Jogador} ({e.Clube})"));
        }
        foreach (var f in faixas.Where(f => f.Total > 0 && f.Feitas == f.Total))
        {
            var chave = $"draft:{f.DraftId}:{PrimeiraDoGrupo}-{UltimaDoGrupo}";
            if (chaves.Contains(chave)) continue;
            var nome = escolhas.First(e => e.DraftId == f.DraftId).NomeDraft;
            novos.Add(Novo(TipoPedidoArte.DraftGrupo, chave, f.DraftId, $"{nome} · escolhas {PrimeiraDoGrupo} a {UltimaDoGrupo}"));
        }

        // Seleção da rodada: rodada de liga (Série A, Série B) com todos os jogos encerrados, o último depois de ligar.
        var rodadas = await _db.LigaRodadas.AsNoTracking()
            .Where(r => r.Liga.Tipo == TipoCompetition.Liga && r.Partidas.Any()
                        && r.Partidas.All(p => p.Status == PartidaStatus.Encerrada)
                        && r.Partidas.Max(p => p.EncerradaEm) >= desde)
            .Select(r => new
            {
                r.RodadaId,
                r.Numero,
                Liga = r.Liga.Nome,
                Notas = _db.LigaNotasJogadores.Count(n => n.Partida.RodadaId == r.RodadaId)
            })
            .ToListAsync(ct);
        foreach (var r in rodadas.Where(r => r.Notas >= 11).OrderBy(r => r.Numero))
        {
            var chave = $"selecao:{r.RodadaId}";
            if (chaves.Contains(chave)) continue;
            novos.Add(Novo(TipoPedidoArte.SelecaoRodada, chave, r.RodadaId, $"Seleção da {r.Numero}ª rodada · {r.Liga}"));
        }

        if (novos.Count == 0) return 0;
        _db.PedidosArte.AddRange(novos);
        await _db.SaveChangesAsync(ct);
        return novos.Count;
    }

    private PedidoArte Novo(TipoPedidoArte tipo, string chave, Guid referencia, string titulo) => new()
    {
        PedidoArteId = Guid.NewGuid(),
        Chave = chave,
        Tipo = tipo,
        ReferenciaId = referencia,
        Titulo = Cortar(titulo, 200),
        Situacao = SituacaoPedidoArte.Pendente,
        CriadoEmUtc = AgoraUtc
    };

    // ── Montar os dados da arte ──────────────────────────────────────────────

    private async Task<PedidoArteDto?> MontarPremioAsync(PedidoArte pedido, CancellationToken ct)
    {
        var c = await _db.PremiosCandidatos.AsNoTracking()
            .Where(x => x.CandidatoId == pedido.ReferenciaId)
            .Select(x => new
            {
                x.Categoria,
                x.Temporada,
                x.PlayerId,
                x.TreinadorId,
                x.Resumo,
                Jogador = x.Player != null ? x.Player.Name : null,
                Treinador = x.Treinador != null ? x.Treinador.Nome : null,
                x.TimeId,
                Clube = x.Time != null ? x.Time.TeamName : null
            })
            .FirstOrDefaultAsync(ct);
        if (c is null) return null;

        var votos = await _db.PremiosVotos.CountAsync(v => v.CandidatoId == pedido.ReferenciaId, ct);
        var regra = PremiosTemporada.De(c.Categoria);
        string? foto = null;
        if (c.PlayerId is int playerId && await _db.FotosJogadores.AnyAsync(f => f.PlayerId == playerId, ct))
            foto = $"/fotos/jogadores/{playerId}";
        else if (c.TreinadorId is Guid treinadorId
                 && await _db.PerfisTreinadores.AnyAsync(p => p.TreinadorId == treinadorId && p.Imagem != null, ct))
            foto = $"/fotos/treinadores/{treinadorId}";

        var numero = c.PlayerId is int jogadorId && c.TimeId is Guid timeId ? await NumeroAsync(jogadorId, timeId, ct) : null;
        var premio = new PremioArteDto(
            regra.Sigla, regra.Nome, c.Temporada, c.Jogador ?? c.Treinador ?? "?", c.Clube,
            numero, votos, c.Resumo, Treinador: c.TreinadorId is not null, foto);
        return new PedidoArteDto(pedido.PedidoArteId, "premio", pedido.Titulo, premio, null);
    }

    private async Task<PedidoArteDto?> MontarContratacaoAsync(PedidoArte pedido, CancellationToken ct)
    {
        var t = await _db.TransferHistories.AsNoTracking()
            .Where(x => x.TransferId == pedido.ReferenciaId)
            .Select(x => new
            {
                x.Type,
                x.Amount,
                x.PlayerId,
                x.ToTeamId,
                Nome = x.Player.Name,
                Over = x.Player.Overall,
                Posicao = x.Player.Position.Name,
                De = x.FromTeam != null ? x.FromTeam.TeamName : null,
                Para = x.ToTeam != null ? x.ToTeam.TeamName : null
            })
            .FirstOrDefaultAsync(ct);
        if (t?.Para is null) return null;

        var foto = await _db.FotosJogadores.AnyAsync(f => f.PlayerId == t.PlayerId, ct) ? $"/fotos/jogadores/{t.PlayerId}" : null;
        var numero = t.ToTeamId is Guid para ? await NumeroAsync(t.PlayerId, para, ct) : null;
        var contratacao = new ContratacaoArteDto(
            Emprestimo: t.Type == TransferType.Loan, t.Nome, t.Para, t.De,
            t.Amount is decimal a && a > 0 ? a : null, numero, t.Over, t.Posicao, foto);
        return new PedidoArteDto(pedido.PedidoArteId, "contratacao", pedido.Titulo, null, contratacao);
    }

    private async Task<PedidoArteDto?> MontarDraftAsync(PedidoArte pedido, CancellationToken ct)
    {
        // a chave diz qual: "draft:{DraftId}:2" ou "draft:{DraftId}:4-8"
        var faixa = pedido.Chave.Split(':').Last().Split('-');
        if (!int.TryParse(faixa[0], out var de)) return null;
        var ate = faixa.Length > 1 && int.TryParse(faixa[1], out var a) ? a : de;

        var draft = await _db.Drafts.AsNoTracking()
            .Where(d => d.DraftId == pedido.ReferenciaId)
            .Select(d => new { d.Name, d.Temporada })
            .FirstOrDefaultAsync(ct);
        if (draft is null) return null;

        var picks = await _db.DraftPicks.AsNoTracking()
            .Where(p => p.DraftId == pedido.ReferenciaId && p.OverallPick >= de && p.OverallPick <= ate && p.PlayerId != null)
            .OrderBy(p => p.OverallPick)
            .Select(p => new
            {
                p.OverallPick,
                p.RoundNumber,
                p.TeamId,
                PlayerId = p.PlayerId!.Value,
                Nome = p.Player!.Name,
                Over = p.Player.Overall,
                Posicao = p.Player.Position.Name,
                Idade = p.Player.Age,
                Clube = p.Team.TeamName
            })
            .ToListAsync(ct);
        if (picks.Count == 0) return null;

        var jogadores = picks.Select(p => p.PlayerId).ToList();
        var comFoto = (await _db.FotosJogadores.AsNoTracking()
            .Where(f => jogadores.Contains(f.PlayerId))
            .Select(f => f.PlayerId).ToListAsync(ct)).ToHashSet();
        var lista = new List<EscolhaDraftArteDto>();
        foreach (var p in picks)
        {
            lista.Add(new EscolhaDraftArteDto(p.OverallPick, p.RoundNumber, p.Nome, p.Clube, p.Over, p.Posicao, p.Idade,
                await NumeroAsync(p.PlayerId, p.TeamId, ct), comFoto.Contains(p.PlayerId) ? $"/fotos/jogadores/{p.PlayerId}" : null));
        }

        var tipo = pedido.Tipo == TipoPedidoArte.DraftGrupo ? "draft-grupo" : "draft";
        return new PedidoArteDto(pedido.PedidoArteId, tipo, pedido.Titulo, null, null,
            new DraftArteDto(draft.Name, draft.Temporada, lista));
    }

    // As vagas do 4-3-3 e, para cada uma, de que posições (na ordem de preferência) sai o jogador.
    private static readonly (string Vaga, string[] Grupos)[] VagasDaSelecao =
    [
        ("GOL", ["GOL"]),
        ("LD", ["LD", "LAT", "ZAG", "MEI"]),
        ("ZAG", ["ZAG", "LAT", "MEI"]),
        ("ZAG", ["ZAG", "LAT", "MEI"]),
        ("LE", ["LE", "LAT", "ZAG", "MEI"]),
        ("MEI", ["MEI", "ATA", "LAT"]),
        ("MEI", ["MEI", "ATA", "LAT"]),
        ("MEI", ["MEI", "ATA", "LAT"]),
        ("PE", ["PE", "ATA", "MEI"]),
        ("CA", ["CA", "ATA", "MEI"]),
        ("PD", ["PD", "ATA", "MEI"]),
    ];

    /// <summary>Os grupos de uma posição do cadastro ("Lateral Direito" → LD e LAT; "Volante" → MEI...).</summary>
    private static string[] GruposDaPosicao(string? posicao)
    {
        var p = (posicao ?? "").ToLowerInvariant();
        if (p.Contains("goleiro")) return ["GOL"];
        if (p.Contains("lateral direito") || p.Contains("ala direito")) return ["LD", "LAT"];
        if (p.Contains("lateral esquerdo") || p.Contains("ala esquerdo")) return ["LE", "LAT"];
        if (p.Contains("lateral")) return ["LAT"];
        if (p.Contains("zagueiro") || p.Contains("líbero")) return ["ZAG"];
        if (p.Contains("ponta esquerda")) return ["PE", "ATA"];
        if (p.Contains("ponta direita")) return ["PD", "ATA"];
        if (p.Contains("centroavante")) return ["CA", "ATA"];
        if (p.Contains("atacante")) return ["ATA", "CA"];
        return ["MEI"];
    }

    private async Task<PedidoArteDto?> MontarSelecaoAsync(PedidoArte pedido, CancellationToken ct)
    {
        var rodada = await _db.LigaRodadas.AsNoTracking()
            .Where(r => r.RodadaId == pedido.ReferenciaId)
            .Select(r => new { r.Numero, Liga = r.Liga.Nome, r.Liga.Temporada })
            .FirstOrDefaultAsync(ct);
        if (rodada is null) return null;

        var notas = await _db.LigaNotasJogadores.AsNoTracking()
            .Where(n => n.Partida.RodadaId == pedido.ReferenciaId)
            .Select(n => new
            {
                n.JogadorId,
                n.PartidaId,
                n.TimeId,
                n.Nota,
                n.MelhorEmCampo,
                Nome = n.Jogador.Name,
                Posicao = n.Jogador.Position.Name,
                Clube = n.Time.TeamName
            })
            .ToListAsync(ct);
        var partidas = notas.Select(n => n.PartidaId).Distinct().ToList();
        var eventos = await _db.LigaEventos.AsNoTracking()
            .Where(e => partidas.Contains(e.PartidaId) && e.Tipo == TipoEvento.Gol)
            .Select(e => new { e.JogadorId, e.AssistenteId })
            .ToListAsync(ct);

        // a melhor nota de cada jogador na rodada (quem jogou duas vezes, no desempate, vale a maior)
        var candidatos = notas
            .GroupBy(n => n.JogadorId)
            .Select(g => g.OrderByDescending(n => n.Nota).First())
            .OrderByDescending(n => n.Nota).ThenByDescending(n => n.MelhorEmCampo)
            .ThenByDescending(n => eventos.Count(e => e.JogadorId == n.JogadorId))
            .ToList();

        var escolhidos = new List<(string Vaga, int JogadorId)>();
        var usados = new HashSet<int>();
        foreach (var (vaga, grupos) in VagasDaSelecao)
        {
            var jogador = grupos
                .Select(g => candidatos.FirstOrDefault(c => !usados.Contains(c.JogadorId) && GruposDaPosicao(c.Posicao).Contains(g)))
                .FirstOrDefault(c => c is not null)
                ?? candidatos.FirstOrDefault(c => !usados.Contains(c.JogadorId) && vaga != "GOL" && !GruposDaPosicao(c.Posicao).Contains("GOL"));
            if (jogador is null) return null;
            usados.Add(jogador.JogadorId);
            escolhidos.Add((vaga, jogador.JogadorId));
        }

        var comFoto = (await _db.FotosJogadores.AsNoTracking()
            .Where(f => usados.Contains(f.PlayerId)).Select(f => f.PlayerId).ToListAsync(ct)).ToHashSet();
        // craque: o primeiro da seleção na ordem dos candidatos (nota, melhor em campo do PES, gols)
        var craqueId = candidatos.First(c => usados.Contains(c.JogadorId)).JogadorId;
        var jogadores = new List<JogadorSelecaoDto>();
        foreach (var (vaga, jogadorId) in escolhidos)
        {
            var c = candidatos.First(x => x.JogadorId == jogadorId);
            jogadores.Add(new JogadorSelecaoDto(vaga, c.Nome, c.Clube, c.Nota,
                eventos.Count(e => e.JogadorId == jogadorId), eventos.Count(e => e.AssistenteId == jogadorId),
                Craque: jogadorId == craqueId,
                await NumeroAsync(jogadorId, c.TimeId, ct), comFoto.Contains(jogadorId) ? $"/fotos/jogadores/{jogadorId}" : null));
        }

        return new PedidoArteDto(pedido.PedidoArteId, "selecao", pedido.Titulo, null, null, null,
            new SelecaoArteDto(rodada.Liga, rodada.Numero, rodada.Temporada, jogadores));
    }

    /// <summary>O número da camisa no clube (aba Camisas); nulo se ainda não escolheram.</summary>
    private async Task<int?> NumeroAsync(int playerId, Guid teamId, CancellationToken ct) =>
        await _db.TeamRosters.AsNoTracking()
            .Where(r => r.PlayerId == playerId && r.TeamId == teamId)
            .Select(r => (int?)r.NumeroCamisa)
            .FirstOrDefaultAsync(ct);

    // ── Admin ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PedidoArteResumoDto>> ListarAsync(int quantos, CancellationToken ct) =>
        await _db.PedidosArte.AsNoTracking()
            .OrderByDescending(p => p.CriadoEmUtc)
            .Take(quantos)
            .Select(p => new PedidoArteResumoDto(p.PedidoArteId, p.Tipo, p.Titulo, p.Situacao, p.Tentativas, p.Erro,
                p.CriadoEmUtc, p.EntregueEmUtc, p.ConcluidoEmUtc))
            .ToListAsync(ct);

    public async Task RefazerAsync(Guid pedidoId, CancellationToken ct)
    {
        var pedido = await AcharAsync(pedidoId, ct);
        pedido.Situacao = SituacaoPedidoArte.Pendente;
        pedido.Tentativas = 0;
        pedido.Erro = null;
        pedido.ConcluidoEmUtc = null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DescartarAsync(Guid pedidoId, CancellationToken ct)
    {
        var pedido = await AcharAsync(pedidoId, ct);
        pedido.Situacao = SituacaoPedidoArte.Descartado;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ArtesConfigDto> ConfigAsync(CancellationToken ct)
    {
        var cfg = await _db.ArtesConfig.AsNoTracking().FirstOrDefaultAsync(ct) ?? new ArtesConfig();
        return new ArtesConfigDto(cfg.Ligado, cfg.ValorMinimo, cfg.OverMinimo, cfg.TransferenciasDesdeUtc);
    }

    public async Task SalvarConfigAsync(bool ligado, decimal valorMinimo, int overMinimo, CancellationToken ct)
    {
        if (valorMinimo < 0) throw new InvalidOperationException("O valor mínimo não pode ser negativo.");
        if (overMinimo is < 40 or > 99) throw new InvalidOperationException("O over mínimo vai de 40 a 99.");

        var cfg = await _db.ArtesConfig.FirstOrDefaultAsync(ct);
        if (cfg is null)
        {
            cfg = new ArtesConfig { Id = 1 };
            _db.ArtesConfig.Add(cfg);
        }

        cfg.Ligado = ligado;
        cfg.ValorMinimo = valorMinimo;
        cfg.OverMinimo = overMinimo;
        // Liga pela primeira vez: as contratações contam a partir de agora.
        if (ligado && cfg.TransferenciasDesdeUtc is null) cfg.TransferenciasDesdeUtc = AgoraUtc;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<PedidoArte> AcharAsync(Guid id, CancellationToken ct) =>
        await _db.PedidosArte.FirstOrDefaultAsync(p => p.PedidoArteId == id, ct)
        ?? throw new KeyNotFoundException("Pedido de arte não encontrado.");

    private static string Cortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];
}
