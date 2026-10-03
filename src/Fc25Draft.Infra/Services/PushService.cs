using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Notificações no celular (Web Push): guarda os aparelhos de cada pessoa e envia os avisos dos times,
/// a vez no draft, o lembrete do bolão e os pacotes do álbum. Aparelho que o serviço de push diz que não existe
/// mais sai sozinho.
/// </summary>
public class PushService : IPushService
{
    /// <summary>Contato do servidor para os serviços de push (VAPID): o endereço do site.</summary>
    private const string Contato = "https://cbfv-app.onrender.com";

    /// <summary>Aviso mais velho que isso não vira notificação (ex.: o site ficou fora do ar).</summary>
    private static readonly TimeSpan AvisoVence = TimeSpan.FromMinutes(30);

    /// <summary>O lembrete do bolão sai para rodadas que começam dentro deste prazo.</summary>
    private static readonly TimeSpan AntecedenciaDoBolao = TimeSpan.FromHours(3);

    /// <summary>O aviso "leilão fechando" sai quando falta isso (ou menos) para o leilão acabar.</summary>
    private static readonly TimeSpan LeilaoFechando = TimeSpan.FromMinutes(15);

    /// <summary>Pacote do álbum mais velho que isso não vira notificação.</summary>
    private static readonly TimeSpan PacoteRecente = TimeSpan.FromDays(1);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<PushService> _logger;

    public PushService(DraftDbContext db, ILogger<PushService> logger, TimeProvider? time = null)
    {
        _db = db;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    private DateTime Agora => _time.GetUtcNow().UtcDateTime;

    public async Task<string> ChavePublicaAsync(CancellationToken ct) => (await ChavesAsync(ct)).PublicKey;

    public async Task InscreverAsync(string? token, InscricaoPushRequest inscricao, CancellationToken ct)
    {
        var pessoa = await PessoaAsync(token, ct);
        if (string.IsNullOrWhiteSpace(inscricao.Endpoint) || !inscricao.Endpoint.StartsWith("https://", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(inscricao.P256dh) || string.IsNullOrWhiteSpace(inscricao.Auth))
            throw new InvalidOperationException("O navegador não mandou uma inscrição válida.");

        // O mesmo aparelho (endpoint) fica com quem ativou por último.
        var existente = await _db.InscricoesPush.FirstOrDefaultAsync(i => i.Endpoint == inscricao.Endpoint, ct);
        if (existente is null)
        {
            existente = new InscricaoPush { InscricaoId = Guid.NewGuid(), Endpoint = inscricao.Endpoint, CriadaEm = Agora };
            _db.InscricoesPush.Add(existente);
        }

        existente.TreinadorId = pessoa;
        existente.P256dh = inscricao.P256dh;
        existente.Auth = inscricao.Auth;
        existente.Aparelho = Cortar(inscricao.Aparelho, 120);
        await _db.SaveChangesAsync(ct);
    }

    public async Task CancelarAsync(string? token, string endpoint, CancellationToken ct)
    {
        var pessoa = await PessoaAsync(token, ct);
        await _db.InscricoesPush.Where(i => i.Endpoint == endpoint && i.TreinadorId == pessoa).ExecuteDeleteAsync(ct);
    }

    public async Task<bool> InscritoAsync(string? token, string endpoint, CancellationToken ct)
    {
        var pessoa = await PessoaOuNuloAsync(token, ct);
        return pessoa is Guid id && await _db.InscricoesPush.AnyAsync(i => i.Endpoint == endpoint && i.TreinadorId == id, ct);
    }

    public async Task<int> EnviarTesteAsync(string? token, CancellationToken ct)
    {
        var pessoa = await PessoaAsync(token, ct);
        var inscricoes = await _db.InscricoesPush.Where(i => i.TreinadorId == pessoa).ToListAsync(ct);
        var enviadas = await EnviarAsync(inscricoes,
            new Notificacao("CBFV", "Notificações ligadas! É assim que os avisos da liga vão chegar.", "/minha-area", "teste"), ct);
        await _db.SaveChangesAsync(ct);
        return enviadas;
    }

    public async Task<int> EnviarAvisosPendentesAsync(CancellationToken ct)
    {
        var avisos = await _db.AvisosTimes
            .Include(a => a.Team)
            .Where(a => a.PushEnviadoEm == null)
            .OrderBy(a => a.CriadoEm)
            .Take(100)
            .ToListAsync(ct);
        if (avisos.Count == 0) return 0;

        // Quem comanda cada time hoje (treinador e auxiliar), com os aparelhos inscritos.
        var times = avisos.Select(a => a.TeamId).Distinct().ToList();
        var aparelhos = await _db.TreinadorPassagens.AsNoTracking()
            .Where(p => p.Ate == null && times.Contains(p.TimeId))
            .Join(_db.InscricoesPush, p => p.TreinadorId, i => i.TreinadorId, (p, i) => new { p.TimeId, i.InscricaoId })
            .ToListAsync(ct);
        var ids = aparelhos.Select(a => a.InscricaoId).Distinct().ToList();
        var inscricoes = await _db.InscricoesPush.Where(i => ids.Contains(i.InscricaoId)).ToDictionaryAsync(i => i.InscricaoId, ct);

        var agora = Agora;
        var enviados = 0;
        foreach (var aviso in avisos)
        {
            aviso.PushEnviadoEm = agora;
            if (agora - Utc(aviso.CriadoEm) > AvisoVence) continue;

            var destino = aparelhos.Where(a => a.TimeId == aviso.TeamId)
                .Select(a => inscricoes.GetValueOrDefault(a.InscricaoId))
                .OfType<InscricaoPush>()
                .Where(i => _db.Entry(i).State != EntityState.Deleted)
                .ToList();
            if (destino.Count == 0) continue;

            await EnviarAsync(destino,
                new Notificacao($"CBFV · {aviso.Team.TeamName}", aviso.Texto, aviso.Link ?? "/minha-area", aviso.AvisoId.ToString()), ct);
            enviados++;
        }

        await _db.SaveChangesAsync(ct);
        return enviados;
    }

    public async Task AvisarVezNoDraftAsync(Guid draftId, string draftNome, int escolha, Guid timeId, CancellationToken ct)
    {
        if (!await MarcarAsync($"draft:{draftId}:{escolha}", ct)) return;

        AvisosDoTime.Criar(_db, timeId, AvisosDoTime.VezNoDraft,
            $"É a sua vez no {draftNome}: escolha nº {escolha}.", "/draft/controle", Agora);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> AvisarLeiloesFechandoAsync(CancellationToken ct)
    {
        var agora = Agora;
        var limite = agora + LeilaoFechando;
        var itens = await _db.MarketItems.AsNoTracking()
            .Where(i => i.Status == MarketItemStatus.Active && i.CurrentLeaderTeamId != null
                        && i.ExpiresAtUtc > agora && i.ExpiresAtUtc <= limite)
            .Select(i => new
            {
                i.ItemId, i.ExpiresAtUtc, Jogador = i.Player.Name, i.CurrentLeaderTeamId, Lider = i.CurrentLeaderTeam!.TeamName,
                i.CurrentLeaderAmount, i.BasePrice, i.MinIncrement, i.BuyNowPrice
            })
            .ToListAsync(ct);
        if (itens.Count == 0) return 0;

        var ids = itens.Select(i => i.ItemId).ToList();
        var quemDeuLance = await _db.MarketBids.AsNoTracking()
            .Where(b => ids.Contains(b.ItemId))
            .Select(b => new { b.ItemId, b.TeamId })
            .Distinct()
            .ToListAsync(ct);

        var brasil = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        var avisados = 0;
        foreach (var item in itens)
        {
            var minutos = Math.Max(1, (int)Math.Ceiling((Utc(item.ExpiresAtUtc) - agora).TotalMinutes));
            var minimo = MarketPricing.ComputeRequiredMinBid(item.BasePrice, item.MinIncrement, item.CurrentLeaderAmount, item.BuyNowPrice);

            foreach (var time in quemDeuLance.Where(b => b.ItemId == item.ItemId && b.TeamId != item.CurrentLeaderTeamId).Select(b => b.TeamId))
            {
                if (!await MarcarAsync($"leilao-fechando:{item.ItemId}:{time}", ct)) continue;
                AvisosDoTime.Criar(_db, time, AvisosDoTime.LeilaoFechando,
                    $"O leilão de {item.Jogador} fecha em {minutos} min e o {item.Lider} está na frente com " +
                    $"{(item.CurrentLeaderAmount ?? 0).ToString("C0", brasil)}. Para cobrir: {minimo.ToString("C0", brasil)}.",
                    "/mercado", agora);
                avisados++;
            }
        }

        if (avisados > 0) await _db.SaveChangesAsync(ct);
        return avisados;
    }

    public async Task<int> LembrarBolaoAsync(CancellationToken ct)
    {
        var pessoas = await _db.InscricoesPush.AsNoTracking()
            .Where(i => i.Treinador.Ativo)
            .Select(i => i.TreinadorId)
            .Distinct()
            .ToListAsync(ct);
        if (pessoas.Count == 0) return 0;

        var bolao = new BolaoService(_db, _time);
        var agora = HorarioDeBrasilia.Agora(_time);
        var lembrados = 0;

        foreach (var pessoa in pessoas)
        {
            var rodadas = (await bolao.RodadasAbertasAsync(pessoa, ct))
                .Where(r => r.Aberta && !r.Completa && r.Quando is DateTime quando
                            && quando > agora && quando - agora <= AntecedenciaDoBolao)
                .ToList();

            foreach (var rodada in rodadas)
            {
                if (!await MarcarAsync($"bolao:{rodada.RodadaId}:{pessoa}", ct)) continue;

                var faltam = rodada.Jogos.Count - rodada.Palpitados;
                var inscricoes = await _db.InscricoesPush.Where(i => i.TreinadorId == pessoa).ToListAsync(ct);
                await EnviarAsync(inscricoes, new Notificacao(
                    "🎯 Bolão da rodada",
                    $"{rodada.Titulo} começa às {rodada.Quando:HH\\:mm} e falta{(faltam == 1 ? "" : "m")} {faltam} palpite{(faltam == 1 ? "" : "s")} seu{(faltam == 1 ? "" : "s")}.",
                    "/bolao", $"bolao-{rodada.RodadaId}"), ct);
                await _db.SaveChangesAsync(ct);
                lembrados++;
            }
        }

        return lembrados;
    }

    public async Task<int> AvisarPacotesGanhosAsync(CancellationToken ct)
    {
        var pessoas = await _db.InscricoesPush.AsNoTracking()
            .Where(i => i.Treinador.Ativo)
            .Select(i => i.TreinadorId)
            .Distinct()
            .ToListAsync(ct);
        if (pessoas.Count == 0) return 0;

        // O pacote do dia e o da reciclagem a pessoa pega no site, não precisam de aviso. Pacote antigo (o site ficou fora do
        // ar, ou a pessoa ativou as notificações agora) também não.
        var desde = Agora - PacoteRecente;
        var pacotes = await _db.PacotesGanhos.AsNoTracking()
            .Where(p => p.AbertoEm == null && p.Origem != PacoteGanho.OrigemDiario && p.Origem != PacoteGanho.OrigemReciclagem && p.CriadoEm >= desde
                        && pessoas.Contains(p.TreinadorId))
            .OrderBy(p => p.CriadoEm)
            .Select(p => new { p.PacoteId, p.TreinadorId, p.Origem, p.Motivo })
            .ToListAsync(ct);
        if (pacotes.Count == 0) return 0;

        var chaves = pacotes.Select(p => ChaveDoPacote(p.PacoteId)).ToList();
        var avisados = (await _db.NotificacoesEnviadas.AsNoTracking()
                .Where(n => chaves.Contains(n.Chave))
                .Select(n => n.Chave)
                .ToListAsync(ct))
            .ToHashSet();

        var pessoasAvisadas = 0;
        foreach (var daPessoa in pacotes.Where(p => !avisados.Contains(ChaveDoPacote(p.PacoteId))).GroupBy(p => p.TreinadorId))
        {
            var novos = new List<(string, string?)>();
            foreach (var p in daPessoa)
                if (await MarcarAsync(ChaveDoPacote(p.PacoteId), ct))
                    novos.Add((p.Origem, p.Motivo));
            if (novos.Count == 0) continue;

            var inscricoes = await _db.InscricoesPush.Where(i => i.TreinadorId == daPessoa.Key).ToListAsync(ct);
            await EnviarAsync(inscricoes, new Notificacao(
                "🎴 Álbum de figurinhas", AlbumFigurinhas.AvisoDePacotes(novos), "/album", "album-pacotes"), ct);
            await _db.SaveChangesAsync(ct);
            pessoasAvisadas++;
        }

        return pessoasAvisadas;
    }

    private static string ChaveDoPacote(Guid pacoteId) => $"pacote:{pacoteId:N}";

    public async Task<int> AvisarConquistasDoAlbumAsync(CancellationToken ct)
    {
        var pessoas = await _db.InscricoesPush.AsNoTracking()
            .Where(i => i.Treinador.Ativo)
            .Select(i => i.TreinadorId)
            .Distinct()
            .ToListAsync(ct);
        if (pessoas.Count == 0) return 0;

        var desde = Agora - PacoteRecente;
        var conquistas = await _db.AlbumConquistas.AsNoTracking()
            .Where(c => c.Em >= desde && pessoas.Contains(c.TreinadorId))
            .OrderBy(c => c.Em)
            .Select(c => new { c.ConquistaId, c.TreinadorId, c.Tipo, Time = c.Time != null ? c.Time.TeamName : null, AlbumNome = c.Album.Nome })
            .ToListAsync(ct);
        if (conquistas.Count == 0) return 0;

        var chaves = conquistas.Select(c => ChaveDaConquista(c.ConquistaId)).ToList();
        var avisadas = (await _db.NotificacoesEnviadas.AsNoTracking()
                .Where(n => chaves.Contains(n.Chave))
                .Select(n => n.Chave)
                .ToListAsync(ct))
            .ToHashSet();

        var pessoasAvisadas = 0;
        foreach (var daPessoa in conquistas.Where(c => !avisadas.Contains(ChaveDaConquista(c.ConquistaId))).GroupBy(c => c.TreinadorId))
        {
            var novas = new List<(TipoConquistaAlbum Tipo, string? Time, string AlbumNome)>();
            foreach (var c in daPessoa)
                if (await MarcarAsync(ChaveDaConquista(c.ConquistaId), ct))
                    novas.Add((c.Tipo, c.Time, c.AlbumNome));
            if (novas.Count == 0) continue;

            var texto = AlbumFigurinhas.AvisoDeConquistas(
                novas[0].AlbumNome,
                novas.Any(n => n.Tipo == TipoConquistaAlbum.AlbumCompleto),
                novas.Where(n => n.Tipo == TipoConquistaAlbum.PaginaCompleta && n.Time is not null).Select(n => n.Time!).ToList());
            var inscricoes = await _db.InscricoesPush.Where(i => i.TreinadorId == daPessoa.Key).ToListAsync(ct);
            await EnviarAsync(inscricoes, new Notificacao("🎴 Álbum de figurinhas", texto, "/album", "album-conquistas"), ct);
            await _db.SaveChangesAsync(ct);
            pessoasAvisadas++;
        }

        return pessoasAvisadas;
    }

    private static string ChaveDaConquista(Guid conquistaId) => $"conquista:{conquistaId:N}";

    public async Task<int> AvisarTrocasAsync(CancellationToken ct)
    {
        var pessoas = (await _db.InscricoesPush.AsNoTracking()
                .Where(i => i.Treinador.Ativo)
                .Select(i => i.TreinadorId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();
        if (pessoas.Count == 0) return 0;

        var desde = Agora - PacoteRecente;
        var trocas = await _db.TrocasFigurinhas.AsNoTracking()
            .Where(t => t.CriadaEm >= desde || t.RespondidaEm >= desde)
            .Select(t => new
            {
                t.TrocaId, t.DeTreinadorId, DeNome = t.De.Nome, t.ParaTreinadorId, ParaNome = t.Para.Nome,
                t.Status, t.ContrapropostaDeId, t.CriadaEm,
                Oferecidas = t.Itens.Count(i => i.Oferecida),
                Pedidas = t.Itens.Count(i => !i.Oferecida)
            })
            .ToListAsync(ct);

        // Quem precisa saber de quê: proposta (ou contraproposta) nova para quem recebeu; aceite e recusa
        // para quem propôs. Proposta já respondida antes do aviso sair não vira aviso de "recebida".
        var eventos = new List<(Guid Pessoa, string Chave, string Evento, string Quem, int Oferecidas, int Pedidas)>();
        foreach (var t in trocas)
        {
            if (t.Status == StatusTroca.Pendente && t.CriadaEm >= desde)
                eventos.Add((t.ParaTreinadorId, $"troca:{t.TrocaId:N}:recebida",
                    t.ContrapropostaDeId is null ? AlbumFigurinhas.EventoTroca.Recebida : AlbumFigurinhas.EventoTroca.Contraproposta,
                    t.DeNome, t.Oferecidas, t.Pedidas));
            else if (t.Status == StatusTroca.Aceita)
                eventos.Add((t.DeTreinadorId, $"troca:{t.TrocaId:N}:aceita", AlbumFigurinhas.EventoTroca.Aceita, t.ParaNome, t.Oferecidas, t.Pedidas));
            else if (t.Status == StatusTroca.Recusada)
                eventos.Add((t.DeTreinadorId, $"troca:{t.TrocaId:N}:recusada", AlbumFigurinhas.EventoTroca.Recusada, t.ParaNome, t.Oferecidas, t.Pedidas));
        }

        eventos = eventos.Where(e => pessoas.Contains(e.Pessoa)).ToList();
        if (eventos.Count == 0) return 0;

        var chaves = eventos.Select(e => e.Chave).ToList();
        var avisados = (await _db.NotificacoesEnviadas.AsNoTracking()
                .Where(n => chaves.Contains(n.Chave))
                .Select(n => n.Chave)
                .ToListAsync(ct))
            .ToHashSet();

        var pessoasAvisadas = 0;
        foreach (var daPessoa in eventos.Where(e => !avisados.Contains(e.Chave)).GroupBy(e => e.Pessoa))
        {
            var novos = new List<(string, string, int, int)>();
            foreach (var e in daPessoa)
                if (await MarcarAsync(e.Chave, ct))
                    novos.Add((e.Evento, e.Quem, e.Oferecidas, e.Pedidas));
            if (novos.Count == 0) continue;

            var inscricoes = await _db.InscricoesPush.Where(i => i.TreinadorId == daPessoa.Key).ToListAsync(ct);
            await EnviarAsync(inscricoes, new Notificacao(
                "🎴 Trocas de figurinhas", AlbumFigurinhas.AvisoDeTrocas(novos), "/album/trocas", "album-trocas"), ct);
            await _db.SaveChangesAsync(ct);
            pessoasAvisadas++;
        }

        return pessoasAvisadas;
    }

    // ── Bastidores ──────────────────────────────────────────────────────────────────────────────────

    private sealed record Notificacao(string Titulo, string Texto, string Link, string Marca);

    /// <summary>Envia a notificação; aparelho que não existe mais (404/410) é removido. Não salva.</summary>
    private async Task<int> EnviarAsync(IReadOnlyCollection<InscricaoPush> inscricoes, Notificacao n, CancellationToken ct)
    {
        if (inscricoes.Count == 0) return 0;

        var chaves = await ChavesAsync(ct);
        using var vapid = new VapidAuthentication(chaves.PublicKey, chaves.PrivateKey) { Subject = Contato };
        var cliente = new PushServiceClient(Http) { DefaultAuthentication = vapid, AutoRetryAfter = false };
        var conteudo = JsonSerializer.Serialize(new { title = n.Titulo, body = n.Texto, url = n.Link, tag = n.Marca });

        var enviadas = 0;
        foreach (var inscricao in inscricoes)
        {
            var assinatura = new PushSubscription { Endpoint = inscricao.Endpoint };
            assinatura.SetKey(PushEncryptionKeyName.P256DH, inscricao.P256dh);
            assinatura.SetKey(PushEncryptionKeyName.Auth, inscricao.Auth);

            try
            {
                await cliente.RequestPushMessageDeliveryAsync(assinatura,
                    new PushMessage(conteudo) { TimeToLive = 12 * 3600, Urgency = PushMessageUrgency.High }, ct);
                inscricao.UltimoEnvioEm = Agora;
                enviadas++;
            }
            catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                // O navegador desinscreveu o aparelho (ou ele foi reinstalado): não adianta mais tentar.
                _db.InscricoesPush.Remove(inscricao);
            }
            catch (Exception ex) when (ex is PushServiceClientException or HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Notificação não entregue ao aparelho {Inscricao}", inscricao.InscricaoId);
            }
        }

        return enviadas;
    }

    private static readonly SemaphoreSlim GerandoChaves = new(1, 1);

    /// <summary>Chaves VAPID do site; na primeira vez gera um par P-256 e guarda.</summary>
    private async Task<ChavePush> ChavesAsync(CancellationToken ct)
    {
        var chaves = await _db.ChavesPush.AsNoTracking().FirstOrDefaultAsync(c => c.ChavePushId == 1, ct);
        if (chaves is not null) return chaves;

        await GerandoChaves.WaitAsync(ct);
        try
        {
            chaves = await _db.ChavesPush.AsNoTracking().FirstOrDefaultAsync(c => c.ChavePushId == 1, ct);
            if (chaves is not null) return chaves;
            chaves = NovasChaves();
            _db.ChavesPush.Add(chaves);
            await _db.SaveChangesAsync(ct);
            _db.Entry(chaves).State = EntityState.Detached;
            return chaves;
        }
        finally
        {
            GerandoChaves.Release();
        }
    }

    private ChavePush NovasChaves()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = ecdsa.ExportParameters(includePrivateParameters: true);
        var publica = new byte[65];
        publica[0] = 0x04;
        p.Q.X!.CopyTo(publica, 1);
        p.Q.Y!.CopyTo(publica, 33);

        return new ChavePush
        {
            ChavePushId = 1,
            PublicKey = Base64Url(publica),
            PrivateKey = Base64Url(p.D!),
            CriadaEm = Agora
        };
    }

    /// <summary>Grava a marca; falso se ela já existia (a notificação já foi feita).</summary>
    private async Task<bool> MarcarAsync(string chave, CancellationToken ct)
    {
        if (await _db.NotificacoesEnviadas.AnyAsync(n => n.Chave == chave, ct)) return false;
        _db.NotificacoesEnviadas.Add(new NotificacaoEnviada { Chave = chave, EnviadaEm = Agora });
        return true;
    }

    private async Task<Guid> PessoaAsync(string? token, CancellationToken ct) =>
        await PessoaOuNuloAsync(token, ct)
        ?? throw new InvalidOperationException("Entre com o seu token de treinador para receber notificações.");

    /// <summary>A pessoa do token: treinador ativo, ou o treinador ligado a um token de admin.</summary>
    private async Task<Guid?> PessoaOuNuloAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var limpo = token.Trim();
        var maiusculo = limpo.ToUpperInvariant();

        var treinador = await _db.Treinadores.AsNoTracking()
            .Where(t => t.Ativo && t.Token.ToUpper() == maiusculo)
            .Select(t => (Guid?)t.TreinadorId)
            .FirstOrDefaultAsync(ct);
        return treinador ?? await _db.AdminTokens.AsNoTracking()
            .Where(a => a.IsActive && a.Token == limpo && a.TreinadorId != null)
            .Select(a => a.TreinadorId)
            .FirstOrDefaultAsync(ct);
    }

    private static DateTime Utc(DateTime d) => d.Kind == DateTimeKind.Local ? d.ToUniversalTime() : d;

    private static string Base64Url(byte[] dados) =>
        Convert.ToBase64String(dados).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string? Cortar(string? texto, int max) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Length <= max ? texto.Trim() : texto[..max].Trim();
}
