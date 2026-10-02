namespace Fc25Draft.Core.Utilities;

/// <summary>
/// As datas das rodadas são gravadas no horário de Brasília (ex.: 18:00), sem fuso. Comparar com o relógio
/// do servidor só funciona se o servidor estiver em Brasília; o Render roda em UTC. Use este "agora".
/// </summary>
public static class HorarioDeBrasilia
{
    public static TimeZoneInfo Fuso { get; } = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "E. South America Standard Time" : "America/Sao_Paulo");

    public static DateTime Agora(TimeProvider relogio) =>
        TimeZoneInfo.ConvertTimeFromUtc(relogio.GetUtcNow().UtcDateTime, Fuso);
}
