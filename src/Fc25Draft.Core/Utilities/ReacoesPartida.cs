namespace Fc25Draft.Core.Utilities;

/// <summary>Os emojis do site para reagir a um jogo e para o chat do telão.</summary>
public static class ReacoesPartida
{
    /// <summary>Os botões de reação de cada jogo, nesta ordem.</summary>
    public static readonly IReadOnlyList<string> Emojis = ["🔥", "😂", "😱", "💀", "👏", "🤡", "😡", "⚽"];

    /// <summary>A paleta do chat do telão.</summary>
    public static readonly IReadOnlyList<string> Chat =
    [
        "⚽", "🔥", "😂", "🤣", "😱", "🤯", "😭", "💀",
        "👏", "🙌", "💪", "😎", "🤡", "😡", "🥶", "🫡",
        "👀", "🙏", "🧤", "🎯", "🟨", "🟥", "🏆", "❤️"
    ];

    public static bool Permitido(string? emoji) => emoji is not null && Emojis.Contains(emoji);
}
