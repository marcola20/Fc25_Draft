using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Web.Utilities;

/// <summary>Cores e textos de resultado (V/E/D) usados na página do time.</summary>
public static class TimeJogoEstilo
{
    public static string Badge(string? resultado) => resultado switch
    {
        "V" => "bg-success",
        "D" => "bg-danger",
        "E" => "bg-secondary",
        _ => "bg-light text-dark"
    };

    public static string Texto(string? resultado) => resultado switch
    {
        "V" => "text-success",
        "D" => "text-danger",
        _ => ""
    };

    public static string Emoji(string? resultado) => resultado switch
    {
        "V" => "🔥",
        "D" => "🥶",
        _ => "🛡️"
    };

    /// <summary>Placar do ponto de vista do time (gols pró primeiro), com W.O. e pênaltis.</summary>
    public static string Placar(TimePerfilJogoDto jogo)
    {
        if (jogo.GolsPro is null || jogo.GolsContra is null)
            return "—";

        var placar = $"{jogo.GolsPro} x {jogo.GolsContra}";
        if (jogo.IsWO)
            placar += " (W.O.)";
        if (jogo.VenceuPenaltis is bool venceu)
            placar += venceu ? " · venceu nos pênaltis" : " · perdeu nos pênaltis";
        return placar;
    }

    public static string Descricao(TimePerfilJogoDto jogo) =>
        $"{Placar(jogo)} {(jogo.EmCasa ? "vs" : "@")} {jogo.AdversarioNome} · {jogo.Competicao} · {jogo.Etapa}";
}
