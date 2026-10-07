using System.Globalization;
using System.Text;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// O resumo da rodada do jeito que vai para o grupo: negrito do WhatsApp com asterisco,
/// uma linha por jogo e só as seções que têm o que dizer.
/// </summary>
public static class ResumoRodadaTexto
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    public static string Montar(ResumoDaRodadaDto resumo)
    {
        var texto = new StringBuilder();

        texto.Append("⚽ *").Append(resumo.Titulo.ToUpper(Br)).AppendLine("*");
        if (resumo.Quando is { } quando)
            texto.AppendLine(quando.ToString("dddd, dd/MM", Br));

        if (!resumo.Completa)
            texto.AppendLine("_(rodada ainda em andamento)_");

        if (resumo.Jogos.Count > 0)
        {
            texto.AppendLine();
            foreach (var jogo in resumo.Jogos)
            {
                texto.Append("*").Append(jogo.Placar).AppendLine("*");

                var gols = Marcadores(jogo);
                if (gols is not null) texto.AppendLine(gols);
            }
        }

        var destaques = Destaques(resumo);
        if (destaques.Count > 0)
        {
            texto.AppendLine();
            texto.AppendLine("🔥 *DESTAQUES*");
            foreach (var linha in destaques) texto.AppendLine(linha);
        }

        if (resumo.Tabela.Count > 0)
        {
            texto.AppendLine();
            texto.AppendLine("📊 *CLASSIFICAÇÃO*");
            foreach (var linha in resumo.Tabela)
                texto.Append(linha.Posicao).Append("º ").Append(linha.TimeNome)
                     .Append(" — ").Append(linha.Pontos).Append(" pts")
                     .Append(" (").Append(linha.Jogos).Append("j, ")
                     .Append(linha.SaldoGols > 0 ? "+" : "").Append(linha.SaldoGols).AppendLine(")");
        }

        if (resumo.Bolao.Count > 0)
        {
            texto.AppendLine();
            texto.AppendLine("🎯 *BOLÃO DA RODADA*");
            foreach (var linha in resumo.Bolao)
            {
                texto.Append(linha.Posicao).Append("º ").Append(linha.Nome)
                     .Append(" — ").Append(linha.Pontos).Append(" pts");

                if (linha.Cravadas > 0)
                    texto.Append(" (").Append(linha.Cravadas).Append(linha.Cravadas == 1 ? " cravada" : " cravadas").Append(')');

                texto.AppendLine();
            }
        }

        if (resumo.Diretoria is { Vazio: false } diretoria)
        {
            texto.AppendLine();
            texto.AppendLine("🏛️ *DIRETORIA*");
            foreach (var m in diretoria.Mudancas)
                texto.Append(DiretoriaLabels.Emoji(m.Depois)).Append(' ').Append(m.TimeNome).Append(": ")
                     .Append(DiretoriaLabels.Faixa(m.Antes)).Append(" → ").Append(DiretoriaLabels.Faixa(m.Depois))
                     .Append(" (").Append(m.Confianca.ToString("0", Br)).AppendLine(")");

            if (diretoria.NaCorda.Count > 0)
                texto.Append("🪑 *Na corda bamba:* ")
                     .AppendLine(string.Join(", ", diretoria.NaCorda.Select(n => $"{n.TimeNome} ({n.Confianca.ToString("0", Br)})")));
        }

        if (resumo.ProximaRodada is { Length: > 0 } proxima)
        {
            texto.AppendLine();
            texto.Append("🗓️ *PRÓXIMA:* ").AppendLine(proxima);
        }

        // Quebra de linha simples: o texto vai para o celular, não para um arquivo do Windows.
        return texto.ToString().Replace("\r\n", "\n").TrimEnd();
    }

    private static string? Marcadores(ResumoJogoDto jogo)
    {
        var casa = string.Join(", ", jogo.MarcadoresCasa);
        var fora = string.Join(", ", jogo.MarcadoresFora);

        if (casa.Length == 0 && fora.Length == 0) return null;

        return $"_{(casa.Length > 0 ? casa : "—")} / {(fora.Length > 0 ? fora : "—")}_";
    }

    private static List<string> Destaques(ResumoDaRodadaDto resumo)
    {
        var linhas = new List<string>();

        if (resumo.Jogos.Count > 0)
        {
            var goleada = resumo.Jogos.OrderByDescending(j => j.Diferenca).ThenByDescending(j => j.TotalDeGols).First();
            if (goleada.Diferenca >= 3)
                linhas.Add($"💥 Goleada: {goleada.Placar}");

            var maisGols = resumo.Jogos.OrderByDescending(j => j.TotalDeGols).First();
            if (maisGols.TotalDeGols >= 5 && maisGols.Placar != goleada.Placar)
                linhas.Add($"🎢 Jogo mais movimentado: {maisGols.Placar}");

            var zeros = resumo.Jogos.Count(j => j.TotalDeGols == 0);
            if (zeros > 0)
                linhas.Add(zeros == 1 ? "🥱 1 jogo sem gols" : $"🥱 {zeros} jogos sem gols");
        }

        foreach (var artilheiro in resumo.Artilheiros.Where(a => a.Gols >= 2))
            linhas.Add($"⚡ {artilheiro.Nome} ({artilheiro.TimeNome}) fez {artilheiro.Gols}");

        return linhas;
    }
}
