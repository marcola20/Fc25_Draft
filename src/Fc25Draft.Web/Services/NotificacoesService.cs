using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Services;

/// <summary>
/// Notificações no celular em segundo plano: a cada 15 s (ou na hora, quando um aviso é gravado) envia os
/// avisos novos dos times, confere de quem é a vez no draft e os leilões fechando; a cada minuto confere quem caiu
/// de faixa na diretoria; a cada 10 min lembra o bolão.
/// </summary>
public class NotificacoesService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(15);
    private const int ChecagensEntreLembretesDoBolao = 40; // 40 × 15 s = 10 min
    private const int ChecagensEntreAvisosDaDiretoria = 4; // 4 × 15 s = 1 min (a conta da diretoria lê todos os jogos)

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NotificacoesService> _logger;
    private readonly SemaphoreSlim _acordar = new(0, 1);

    public NotificacoesService(IServiceScopeFactory scopes, ILogger<NotificacoesService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DraftDbContext.AvisosGravados += Acordar;
        try
        {
            for (var checagem = 0; !stoppingToken.IsCancellationRequested; checagem++)
            {
                await ChecarAsync(
                    lembrarBolao: checagem % ChecagensEntreLembretesDoBolao == 0,
                    diretoria: checagem % ChecagensEntreAvisosDaDiretoria == 0,
                    stoppingToken);

                try
                {
                    await _acordar.WaitAsync(Intervalo, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            DraftDbContext.AvisosGravados -= Acordar;
        }
    }

    // Aviso novo gravado: adianta a próxima checagem (sem empilhar sinais).
    private void Acordar(IReadOnlyCollection<Guid> _)
    {
        if (_acordar.CurrentCount == 0)
        {
            try { _acordar.Release(); } catch (SemaphoreFullException) { }
        }
    }

    private async Task ChecarAsync(bool lembrarBolao, bool diretoria, CancellationToken ct)
    {
        // Uma falha numa parte não pode parar as outras nem o serviço.
        await TentarAsync("vez no draft", s => AvisarVezNoDraftAsync(s, ct));
        // Antes dos avisos: o que a diretoria gravar sai como notificação nesta mesma checagem.
        if (diretoria)
            await TentarAsync("diretoria", s => s.GetRequiredService<IDiretoriaService>().ProcessarJogosRecentesAsync(ct));
        await TentarAsync("leilões fechando", s => s.GetRequiredService<IPushService>().AvisarLeiloesFechandoAsync(ct));
        await TentarAsync("avisos", s => s.GetRequiredService<IPushService>().EnviarAvisosPendentesAsync(ct));
        if (lembrarBolao)
            await TentarAsync("bolão", s => s.GetRequiredService<IPushService>().LembrarBolaoAsync(ct));

        async Task TentarAsync(string parte, Func<IServiceProvider, Task> acao)
        {
            try
            {
                // Um escopo por checagem: contexto novo, sem nada rastreado de antes.
                await using var scope = _scopes.CreateAsyncScope();
                await acao(scope.ServiceProvider);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Falha nas notificações ({Parte})", parte);
            }
        }
    }

    /// <summary>
    /// Avisa o time da vez quando o draft está rolando: relógio já começou ou alguém já escolheu.
    /// Draft criado e ainda não começado não avisa ninguém.
    /// </summary>
    private static async Task AvisarVezNoDraftAsync(IServiceProvider servicos, CancellationToken ct)
    {
        var estado = await servicos.GetRequiredService<DraftStateService>().GetStateAsync(ct);
        if (estado is not { HasDraft: true, DraftCompleted: false, AguardandoProtecao: false }
            || estado.CurrentTeamId is not Guid time || estado.CurrentOverallPick is not int escolha)
            return;

        if (estado.CompletedPicks == 0)
        {
            var db = servicos.GetRequiredService<DraftDbContext>();
            var comecou = await db.Drafts.AsNoTracking()
                .AnyAsync(d => d.DraftId == estado.DraftId && d.VezIniciadaEm != null, ct);
            if (!comecou) return;
        }

        await servicos.GetRequiredService<IPushService>()
            .AvisarVezNoDraftAsync(estado.DraftId!.Value, estado.DraftName ?? "draft", escolha, time, ct);
    }
}
