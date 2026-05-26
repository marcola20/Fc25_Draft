namespace Fc25Draft.Web.Services;

public sealed record LotteryPickItem(int PickNumero, Guid TimeId, string TimeNome, int Posicao);

public sealed class LotteryState
{
    public Guid LigaId { get; init; }
    public string LigaNome { get; init; } = "";
    public Guid? CopaLigaId { get; init; }
    public string CopaLigaNome { get; init; } = "";
    public int Rodada { get; set; }
    public bool IsFinished { get; set; }
    public List<LotteryPickItem> Revealed { get; } = [];
    public List<LotteryPickItem> Pending { get; set; } = [];
    public List<LotteryPickItem> Round1Results { get; set; } = [];  // salvo ao avançar para round 2
    public LotteryPickItem? LastRevealed => Revealed.Count > 0 ? Revealed[^1] : null;
    public bool HasPending => Pending.Count > 0;
    public bool Round1Done => Rodada == 1 && !HasPending;
    public bool Round2Done => Rodada == 2 && !HasPending;
}

/// <summary>
/// Singleton — holds live lottery state and notifies all subscribed Blazor circuits.
/// </summary>
public sealed class LotteryStateService
{
    private readonly object _lock = new();

    public LotteryState? State { get; private set; }

    public event Action? OnChange;

    private void Notify() => OnChange?.Invoke();

    public void Start(LotteryState state)
    {
        lock (_lock) { State = state; }
        Notify();
    }

    public void RevealNext()
    {
        lock (_lock)
        {
            if (State is null || State.Pending.Count == 0) return;
            var next = State.Pending[0];
            State.Pending.RemoveAt(0);
            State.Revealed.Add(next);
        }
        Notify();
    }

    public void AdvanceToRound2(List<LotteryPickItem> picks)
    {
        lock (_lock)
        {
            if (State is null) return;
            State.Round1Results = State.Revealed.OrderBy(p => p.PickNumero).ToList();
            State.Rodada = 2;
            State.Revealed.Clear();
            State.Pending = picks;
        }
        Notify();
    }

    public void Finish()
    {
        lock (_lock)
        {
            if (State is null) return;
            State.IsFinished = true;
            State.Pending.Clear();
        }
        Notify();
    }

    public void Reset()
    {
        lock (_lock) { State = null; }
        Notify();
    }
}
