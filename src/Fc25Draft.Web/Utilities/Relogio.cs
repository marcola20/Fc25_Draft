namespace Fc25Draft.Web.Utilities;

public static class Relogio
{
    /// <summary>"04:59" até uma hora; acima disso "2h05"; negativo vira "00:00".</summary>
    public static string Formatar(TimeSpan restante)
    {
        if (restante <= TimeSpan.Zero)
            return "00:00";

        return restante.TotalHours >= 1
            ? $"{(int)restante.TotalHours}h{restante.Minutes:00}"
            : $"{restante.Minutes:00}:{restante.Seconds:00}";
    }
}
