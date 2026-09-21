using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Web.Services;

/// <summary>Um time saindo do pote para o grupo, na ordem em que aparece no sorteio ao vivo.</summary>
public sealed record CopaSorteioRevelacao(int Ordem, int Pote, GrupoCopa Grupo, Guid TimeId, string TimeNome);

public sealed class CopaSorteioAoVivo
{
    public required Guid LigaId { get; init; }
    public required string LigaNome { get; init; }
    public required IReadOnlyList<int> TamanhosDosGrupos { get; init; }
    public required IReadOnlyList<CopaSorteioRevelacao> Sequencia { get; init; }
    public required TimeSpan Intervalo { get; init; }

    public int Reveladas { get; set; }
    public DateTime? ProximaEmUtc { get; set; }

    public bool Terminado => Reveladas >= Sequencia.Count;
    public IEnumerable<CopaSorteioRevelacao> JaReveladas => Sequencia.Take(Reveladas);
    public CopaSorteioRevelacao? Ultima => Reveladas > 0 ? Sequencia[Reveladas - 1] : null;
    public CopaSorteioRevelacao? Proxima => Terminado ? null : Sequencia[Reveladas];
}

/// <summary>
/// Singleton que conduz o sorteio da Copa ao vivo: revela um time a cada intervalo e avisa
/// todas as telas abertas ao mesmo tempo (mesmo esquema da loteria do draft).
/// Os grupos já estão gravados no banco antes de começar; aqui só se controla o que já apareceu.
/// </summary>
public sealed class CopaSorteioAoVivoService : IDisposable
{
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;

    public CopaSorteioAoVivo? Estado { get; private set; }

    public event Action? OnChange;

    public bool EmAndamento
    {
        get { lock (_lock) return Estado is { Terminado: false }; }
    }

    /// <summary>
    /// Monta a ordem de revelação: pote 1 inteiro (cabeças de chave), depois pote 2, 3, 4...
    /// Dentro do pote, os grupos são preenchidos em ordem (A, B, C, D) e, quando o pote tem
    /// mais times que grupos, a segunda leva vem em seguida.
    /// </summary>
    public static List<CopaSorteioRevelacao> MontarSequencia(LigaCopaSorteioDto sorteio)
    {
        var ordem = 0;
        var sequencia = new List<CopaSorteioRevelacao>();

        foreach (var pote in sorteio.Times.Where(t => t.Grupo is not null).GroupBy(t => t.Pote).OrderBy(p => p.Key))
        {
            var porGrupo = pote
                .GroupBy(t => t.Grupo!.Value)
                .SelectMany(g => g.OrderBy(_ => Random.Shared.Next()).Select((t, leva) => (Leva: leva, t.Grupo, t.TimeId, t.TimeNome)))
                .OrderBy(x => x.Leva)
                .ThenBy(x => x.Grupo);

            foreach (var t in porGrupo)
                sequencia.Add(new CopaSorteioRevelacao(++ordem, pote.Key, t.Grupo!.Value, t.TimeId, t.TimeNome));
        }

        return sequencia;
    }

    public void Iniciar(Guid ligaId, string ligaNome, LigaCopaSorteioDto sorteio, TimeSpan intervalo)
    {
        CancelarLoop();

        var estado = new CopaSorteioAoVivo
        {
            LigaId = ligaId,
            LigaNome = ligaNome,
            TamanhosDosGrupos = sorteio.TamanhosDosGrupos,
            Sequencia = MontarSequencia(sorteio),
            Intervalo = intervalo,
            ProximaEmUtc = DateTime.UtcNow + intervalo
        };

        var cts = new CancellationTokenSource();
        lock (_lock)
        {
            Estado = estado;
            _cts = cts;
        }

        Notificar();
        _ = RodarAsync(estado, cts.Token);
    }

    private async Task RodarAsync(CopaSorteioAoVivo estado, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(estado.Intervalo, ct);

                lock (_lock)
                {
                    if (!ReferenceEquals(Estado, estado) || estado.Terminado) return;
                    estado.Reveladas++;
                    estado.ProximaEmUtc = estado.Terminado ? null : DateTime.UtcNow + estado.Intervalo;
                }

                Notificar();
                if (estado.Terminado) return;
            }
        }
        catch (OperationCanceledException)
        {
            // Sorteio cancelado ou revelado de uma vez pelo admin.
        }
    }

    /// <summary>Mostra tudo que falta de uma vez.</summary>
    public void RevelarTudo()
    {
        CancelarLoop();
        lock (_lock)
        {
            if (Estado is null) return;
            Estado.Reveladas = Estado.Sequencia.Count;
            Estado.ProximaEmUtc = null;
        }
        Notificar();
    }

    /// <summary>Tira o sorteio da tela (os grupos gravados continuam como estão).</summary>
    public void Limpar()
    {
        CancelarLoop();
        lock (_lock) { Estado = null; }
        Notificar();
    }

    private void CancelarLoop()
    {
        CancellationTokenSource? cts;
        lock (_lock)
        {
            cts = _cts;
            _cts = null;
        }
        cts?.Cancel();
        cts?.Dispose();
    }

    private void Notificar() => OnChange?.Invoke();

    public void Dispose() => CancelarLoop();
}
