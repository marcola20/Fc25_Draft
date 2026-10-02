using Fc25Draft.Core.Interfaces;

namespace Fc25Draft.Web.Services;

/// <summary>
/// A cada 10 minutos expira as propostas pendentes há mais de 48 horas (quem mandou recebe aviso).
/// O aceite já recusa proposta vencida mesmo entre uma checagem e outra.
/// </summary>
public class PropostasExpiradasService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PropostasExpiradasService> _logger;

    public PropostasExpiradasService(IServiceScopeFactory scopes, ILogger<PropostasExpiradasService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);
        do
        {
            try
            {
                // Um escopo por checagem: contexto novo, sem nada rastreado de antes.
                await using var scope = _scopes.CreateAsyncScope();
                var expiradas = await scope.ServiceProvider.GetRequiredService<ITransferOfferService>()
                    .ExpirarAntigasAsync(stoppingToken);
                if (expiradas > 0)
                    _logger.LogInformation("Propostas expiradas: {Quantidade}", expiradas);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Falha ao expirar propostas antigas");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
