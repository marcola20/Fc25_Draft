using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Services;

namespace Fc25Draft.Web.Services;

/// <summary>
/// Pacotes do álbum em segundo plano: a cada 5 min dá os que faltam pelas vitórias e pelo bolão (o
/// livro-razão de pacotes não deixa duplicar) e avisa no celular quem ganhou pacote ou selo. Selo novo
/// (página ou álbum completo) adianta a rodada, para o aviso sair na hora.
/// </summary>
public class PacotesDoAlbumService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PacotesDoAlbumService> _logger;
    private readonly SemaphoreSlim _acordar = new(0, 1);

    public PacotesDoAlbumService(IServiceScopeFactory scopes, ILogger<PacotesDoAlbumService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        AlbumService.ConquistasGravadas += Acordar;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RodarAsync(stoppingToken);

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
            AlbumService.ConquistasGravadas -= Acordar;
        }
    }

    // Selo novo: adianta a próxima rodada (sem empilhar sinais).
    private void Acordar()
    {
        if (_acordar.CurrentCount == 0)
        {
            try { _acordar.Release(); } catch (SemaphoreFullException) { }
        }
    }

    private async Task RodarAsync(CancellationToken ct)
    {
        // Uma falha numa parte não pode parar as outras nem o serviço.
        await TentarAsync("reconciliação", async s =>
        {
            var criados = await s.GetRequiredService<IAlbumService>().ReconciliarAsync(ct);
            if (criados.Total > 0)
                _logger.LogInformation("Pacotes do álbum: {Vitorias} por vitória e {Bolao} do bolão", criados.Vitorias, criados.Bolao);
        });
        await TentarAsync("aviso de pacotes", s => s.GetRequiredService<IPushService>().AvisarPacotesGanhosAsync(ct));
        await TentarAsync("aviso de selos", s => s.GetRequiredService<IPushService>().AvisarConquistasDoAlbumAsync(ct));

        async Task TentarAsync(string parte, Func<IServiceProvider, Task> acao)
        {
            try
            {
                // Um escopo por execução: contexto novo, sem nada rastreado de antes.
                await using var scope = _scopes.CreateAsyncScope();
                await acao(scope.ServiceProvider);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Falha nos pacotes do álbum ({Parte})", parte);
            }
        }
    }
}
