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
/// a vez no draft e o lembrete do bolão. Aparelho que o serviço de push diz que não existe mais sai sozinho.
/// </summary>
public class PushService : IPushService
{
    /// <summary>Contato do servidor para os serviços de push (VAPID): o endereço do site.</summary>
    private const string Contato = "https://cbfv-app.onrender.com";

    /// <summary>Aviso mais velho que isso não vira notificação (ex.: o site ficou fora do ar).</summary>
    private static readonly TimeSpan AvisoVence = TimeSpan.FromMinutes(30);

    /// <summary>O lembrete do bolão sai para rodadas que começam dentro deste prazo.</summary>
    private static readonly TimeSpan AntecedenciaDoBolao = TimeSpan.FromHours(3);

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
