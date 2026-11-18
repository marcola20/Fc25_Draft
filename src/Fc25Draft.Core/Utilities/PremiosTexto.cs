using System.Text;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Os prêmios da temporada do jeito que vão para o grupo: um texto por prêmio (para soltar um de cada vez) e um
/// com todos juntos. Negrito e itálico do WhatsApp.
/// </summary>
public static class PremiosTexto
{
    /// <summary>Um prêmio: vencedor(es) com votos e números, o +1 over e quem mais concorreu.</summary>
    public static string Premio(PremioCategoriaDto categoria, int temporada, string? link = null)
    {
        var regra = categoria.Regra;
        var texto = new StringBuilder();

        texto.Append(regra.Emoji).Append(" *").Append(PremiosTemporada.Titulo(regra.Categoria, temporada)).AppendLine("*");
        texto.Append('_').Append(regra.Nome).Append(categoria.Vencedores.Count > 1 ? " · eleitos" : " · eleito").AppendLine(" pelos treinadores_");

        foreach (var c in categoria.Vencedores.OrderByDescending(c => c.Votos ?? 0).ThenBy(c => c.Nome))
        {
            texto.AppendLine();
            texto.Append("🏆 *").Append(c.Nome).Append('*');
            if (c.TimeNome is { Length: > 0 } time) texto.Append(" — ").Append(time);
            texto.AppendLine();

            var detalhes = new List<string> { Votos(c.Votos) };
            if (c.Resumo is { Length: > 0 } resumo) detalhes.Add(resumo);
            texto.AppendLine(string.Join(" · ", detalhes));

            if (Over(c, regra.DaOver) is { } over) texto.AppendLine(over);
        }

        var outros = categoria.Candidatos.Where(c => !c.Vencedor && (c.Votos ?? 0) > 0)
            .OrderByDescending(c => c.Votos).ThenBy(c => c.Nome)
            .Select(c => $"{c.Nome} ({c.Votos})")
            .ToList();
        if (outros.Count > 0)
        {
            texto.AppendLine();
            texto.Append("_Também votados: ").Append(string.Join(", ", outros)).AppendLine("_");
        }

        if (link is not null)
        {
            texto.AppendLine();
            texto.AppendLine(link);
        }

        return texto.ToString().TrimEnd();
    }

    /// <summary>Todos os vencedores numa mensagem só.</summary>
    public static string Todos(VotacaoPremiosDto votacao, string? link = null)
    {
        var texto = new StringBuilder();
        texto.Append("🏆 *PRÊMIOS DA TEMPORADA ").Append(votacao.Temporada).AppendLine("*");
        texto.Append("_Eleitos pelos treinadores · ").Append(votacao.Votaram)
             .Append(votacao.Votaram == 1 ? " votou_" : " votaram_").AppendLine();

        foreach (var categoria in votacao.Categorias)
        {
            var vencedores = categoria.Vencedores.OrderByDescending(c => c.Votos ?? 0).ThenBy(c => c.Nome).ToList();
            if (vencedores.Count == 0) continue;

            texto.AppendLine();
            texto.Append(categoria.Regra.Emoji).Append(" *").Append(categoria.Regra.Sigla).Append("* — ").AppendLine(categoria.Regra.Nome);
            foreach (var c in vencedores)
            {
                texto.Append("• *").Append(c.Nome).Append('*');
                if (c.TimeNome is { Length: > 0 } time) texto.Append(" (").Append(time).Append(')');
                texto.Append(" · ").AppendLine(Votos(c.Votos));
            }
        }

        if (votacao.Categorias.Any(c => c.Regra.DaOver))
        {
            texto.AppendLine();
            texto.AppendLine("⬆️ Os vencedores de jogador ganham +1 de over.");
        }

        if (link is not null)
        {
            texto.AppendLine();
            texto.AppendLine(link);
        }

        return texto.ToString().TrimEnd();
    }

    private static string Votos(int? votos) => (votos ?? 0) == 1 ? "1 voto" : $"{votos ?? 0} votos";

    private static string? Over(PremioCandidatoDto c, bool daOver)
    {
        if (!daOver) return null;
        // Pendente: ganhou dois prêmios e o admin ainda não escolheu o companheiro.
        if (c.OverAplicadoEm is null) return "⬆️ +1 de over vai para um companheiro de clube";
        return c.OverParaPlayerId is int para && para != c.PlayerId
            ? $"⬆️ +1 de over repassado para *{c.OverParaNome}*"
            : "⬆️ Ganha +1 de over";
    }
}
