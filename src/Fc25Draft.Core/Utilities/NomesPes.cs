using System.Globalization;
using System.Text;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Casa nomes vindos do PES 2021 (lidos por OCR) com o cadastro: times pelo nome
/// normalizado + apelidos, jogadores por aproximação dentro de uma lista de candidatos.
/// Na dúvida não casa — melhor "não identificado" do que gol no jogador errado.
/// </summary>
public static class NomesPes
{
    public record Candidato(int Id, string Nome);

    /// <summary>Id nulo = não casou; Motivo explica por quê.</summary>
    public record Casamento(int? Id, string? Motivo);

    // Nome do time no PES → nome no cadastro, quando diferem (chaves já normalizadas).
    private static readonly Dictionary<string, string> ApelidosTimes = new()
    {
        ["inter"] = "internacional",
        ["ec juventude"] = "juventude",
    };

    // Pontuação mínima para aceitar: sobrenome igual (ex.: "Kun Agüero" x "Sergio Agüero").
    private const int PontuacaoMinima = 60;

    public static bool MesmoTime(string? nomePes, string nomeCadastro) =>
        !string.IsNullOrWhiteSpace(nomePes) && ChaveTime(nomePes) == ChaveTime(nomeCadastro);

    private static string ChaveTime(string nome)
    {
        var chave = Normalizar(nome);
        return ApelidosTimes.GetValueOrDefault(chave, chave);
    }

    /// <summary>Sem acento, minúsculo, sem apóstrofo; outra pontuação vira espaço.</summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";

        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (c is '\'' or '’' or '‘' or '`' or '´') continue; // D’Alessandro = DAlessandro
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static Casamento Casar(string? nomePes, IEnumerable<Candidato> candidatos)
    {
        var alvo = Normalizar(nomePes);
        if (alvo.Length == 0)
            return new Casamento(null, "nome ilegível no vídeo");

        var pontuados = candidatos
            .DistinctBy(c => c.Id)
            .Select(c => (c, Pontos: Pontuar(alvo, Normalizar(c.Nome))))
            .Where(x => x.Pontos >= PontuacaoMinima)
            .ToList();

        if (pontuados.Count == 0)
            return new Casamento(null, "nenhum jogador parecido no elenco");

        var melhor = pontuados.Max(x => x.Pontos);
        var empatados = pontuados.Where(x => x.Pontos == melhor).Select(x => x.c).ToList();
        if (empatados.Count > 1)
            return new Casamento(null, "ambíguo: " + string.Join(", ", empatados.Select(c => c.Nome).OrderBy(n => n)));

        return new Casamento(empatados[0].Id, null);
    }

    /// <summary>
    /// 100 = igual; 95 = igual sem espaços ("Van der Sar" x "Vandersar");
    /// 80 = um nome contido no outro ("Kun Agüero" x "Agüero", "Thiago" x "Thiago Alcântara");
    /// 60 = mesmo sobrenome ("Leo Messi" x "Lionel Messi"); 0 = diferente.
    /// </summary>
    private static int Pontuar(string a, string b)
    {
        if (b.Length == 0) return 0;
        if (a == b) return 100;
        if (a.Replace(" ", "") == b.Replace(" ", "")) return 95;

        var ta = a.Split(' ');
        var tb = b.Split(' ');
        var (curto, longo) = ta.Length <= tb.Length ? (ta, tb) : (tb, ta);

        // Contido: toda palavra do nome curto está no longo, e ao menos uma não é só inicial.
        if (curto.All(p => longo.Any(q => MesmaPalavra(p, q))) && curto.Any(p => p.Length >= 3))
            return 80;

        // Sobrenome: só a última palavra — "Thiago Silva" não pode virar "Thiago Alcântara".
        if (ta[^1].Length >= 4 && MesmaPalavra(ta[^1], tb[^1]))
            return 60;

        return 0;
    }

    /// <summary>Igual; inicial ("J." x "Júlio"); ou um erro de OCR em palavra de 5+ letras.</summary>
    private static bool MesmaPalavra(string a, string b)
    {
        if (a == b) return true;
        if (a.Length == 1 || b.Length == 1) return a[0] == b[0];
        return Math.Min(a.Length, b.Length) >= 5 && DistanciaAteUm(a, b);
    }

    private static bool DistanciaAteUm(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 1) return false;
        int i = 0, j = 0, diferencas = 0;
        while (i < a.Length && j < b.Length)
        {
            if (a[i] == b[j]) { i++; j++; continue; }
            if (++diferencas > 1) return false;
            if (a.Length > b.Length) i++;
            else if (b.Length > a.Length) j++;
            else { i++; j++; }
        }
        return diferencas + (a.Length - i) + (b.Length - j) <= 1;
    }
}
