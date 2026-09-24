using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Web.Services;

/// <summary>
/// Última sequência de escolhas feitas porque o tempo acabou. Ninguém clicou nada, então a mensagem para o
/// WhatsApp fica aqui para a página do draft mostrar (em memória: some se o site reiniciar).
/// </summary>
public class DraftAvisoTempoEsgotado
{
    private readonly object _lock = new();
    private (DraftPickSelectionDto Selecao, DateTime Em)? _ultimo;

    public void Registrar(DraftPickSelectionDto selecao)
    {
        lock (_lock)
        {
            _ultimo = (selecao, DateTime.UtcNow);
        }
    }

    public (DraftPickSelectionDto Selecao, DateTime Em)? Ultimo
    {
        get
        {
            lock (_lock)
            {
                return _ultimo;
            }
        }
    }
}

/// <summary>
/// Relógio do draft: a cada poucos segundos confere se o tempo da vez acabou e, se acabou, escolhe pelo time
/// (lista da escolha automática ou melhor overall) e segue a sequência automática.
/// </summary>
public class DraftRelogioService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopes;
    private readonly DraftAvisoTempoEsgotado _aviso;
    private readonly ILogger<DraftRelogioService> _logger;

    public DraftRelogioService(IServiceScopeFactory scopes, DraftAvisoTempoEsgotado aviso, ILogger<DraftRelogioService> logger)
    {
        _scopes = scopes;
        _aviso = aviso;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // Um escopo por checagem: contexto novo, sem nada rastreado de antes.
                await using var scope = _scopes.CreateAsyncScope();
                var draft = scope.ServiceProvider.GetRequiredService<DraftStateService>();
                var resultado = await draft.ProcessarTempoEsgotadoAsync(stoppingToken);

                if (resultado?.Selection is { } selecao)
                {
                    _aviso.Registrar(selecao);
                    _logger.LogInformation("Tempo esgotado no draft: {Escolhas} escolha(s) feitas pelo relógio.", resultado.Escolhas?.Count ?? 1);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Uma falha não pode parar o relógio: tenta de novo na próxima checagem.
                _logger.LogError(ex, "Falha ao conferir o relógio do draft.");
            }
        }
    }
}
