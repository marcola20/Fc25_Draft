using System.Text.RegularExpressions;

namespace Fc25Draft.Core.Utilities;

/// <summary>Tira o ID do vídeo de um link do YouTube, seja qual for o formato que o admin colar.</summary>
public static partial class YoutubeLink
{
    // IDs de vídeo do YouTube têm 11 caracteres: letras, números, '-' e '_'.
    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex IdValido();

    [GeneratedRegex(@"(?:youtu\.be/|youtube(?:-nocookie)?\.com/(?:watch\?(?:.*&)?v=|embed/|live/|shorts/|v/))([A-Za-z0-9_-]{11})")]
    private static partial Regex IdNoLink();

    /// <summary>
    /// Aceita youtube.com/watch?v=, youtu.be/, /live/, /embed/, /shorts/ ou o ID puro.
    /// Devolve null quando não reconhece.
    /// </summary>
    public static string? ExtrairId(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return null;

        var limpo = link.Trim();
        if (IdValido().IsMatch(limpo)) return limpo;

        var m = IdNoLink().Match(limpo);
        return m.Success ? m.Groups[1].Value : null;
    }

    public static string Assistir(string videoId) => $"https://www.youtube.com/watch?v={videoId}";
}
