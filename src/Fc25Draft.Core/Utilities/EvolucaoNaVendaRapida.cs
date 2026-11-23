using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// A tabela de evolução da venda rápida: quanto sobe cada overall. A de fábrica vale enquanto o admin não salvar
/// nada em /admin/venda-rapida.
/// </summary>
public static class EvolucaoNaVendaRapida
{
    /// <summary>Primeiro overall que aparece na tabela (abaixo dele, ninguém evolui).</summary>
    public const int PrimeiroOverall = 60;

    /// <summary>Último overall que aparece na tabela (99 não tem para onde subir).</summary>
    public const int UltimoOverall = 98;

    /// <summary>Maior subida aceita numa linha.</summary>
    public const int SubidaMaxima = 20;

    /// <summary>A tabela de fábrica: a regra antiga, com +1 para quem tem 87 ou mais.</summary>
    public static IReadOnlyList<EvolucaoVendaRapidaDto> Fabrica { get; } =
        Enumerable.Range(PrimeiroOverall, UltimoOverall - PrimeiroOverall + 1)
            .Select(o =>
            {
                var (min, max) = o switch
                {
                    >= 87 => (1, 1),
                    >= 85 => (1, 2),
                    >= 83 => (2, 3),
                    >= 81 => (3, 3),
                    80 => (4, 4),
                    79 => (4, 5),
                    >= 77 => (5, 6),
                    >= 75 => (7, 7),
                    _ => (0, 0),
                };
                return new EvolucaoVendaRapidaDto(o, min, max);
            })
            .ToList();

    /// <summary>Quanto o overall pode subir (mínimo, máximo); 0/0 se não evolui ou não está na tabela.</summary>
    public static (int Minimo, int Maximo) Subida(int overall, IReadOnlyList<EvolucaoVendaRapidaDto> tabela)
    {
        var linha = tabela.FirstOrDefault(l => l.Overall == overall);
        return linha is null ? (0, 0) : (linha.Minimo, linha.Maximo);
    }

    /// <summary>Erro de uma tabela a salvar, ou null se está certa.</summary>
    public static string? Validar(IReadOnlyList<EvolucaoVendaRapidaDto> tabela)
    {
        foreach (var l in tabela)
        {
            if (l.Overall is < 1 or > 99)
                return $"Overall {l.Overall} fora da faixa (1 a 99).";
            if (l.Minimo < 0 || l.Maximo < 0)
                return $"Overall {l.Overall}: a subida não pode ser negativa.";
            if (l.Minimo > l.Maximo)
                return $"Overall {l.Overall}: o mínimo ({l.Minimo}) é maior que o máximo ({l.Maximo}).";
            if (l.Maximo > SubidaMaxima)
                return $"Overall {l.Overall}: a subida máxima é +{SubidaMaxima}.";
        }

        var repetido = tabela.GroupBy(l => l.Overall).FirstOrDefault(g => g.Count() > 1);
        return repetido is null ? null : $"Overall {repetido.Key} aparece duas vezes.";
    }

    /// <summary>
    /// A tabela em faixas para mostrar ("75 e 76 → +7"): overalls seguidos com a mesma subida viram uma linha.
    /// </summary>
    public static IReadOnlyList<(string Overall, string Sobe)> Faixas(IReadOnlyList<EvolucaoVendaRapidaDto> tabela)
    {
        var linhas = tabela.OrderBy(l => l.Overall).ToList();
        var faixas = new List<(string, string)>();
        var i = 0;
        while (i < linhas.Count)
        {
            var j = i;
            while (j + 1 < linhas.Count
                   && linhas[j + 1].Overall == linhas[j].Overall + 1
                   && linhas[j + 1].Minimo == linhas[i].Minimo
                   && linhas[j + 1].Maximo == linhas[i].Maximo)
                j++;

            int de = linhas[i].Overall, ate = linhas[j].Overall;
            var overall = i == 0 && de <= PrimeiroOverall ? $"até {ate}"
                : j == linhas.Count - 1 && ate >= UltimoOverall ? $"{de} ou mais"
                : de == ate ? $"{de}"
                : ate == de + 1 ? $"{de} e {ate}"
                : $"{de} a {ate}";
            faixas.Add((overall, Descrever(linhas[i].Minimo, linhas[i].Maximo)));
            i = j + 1;
        }
        return faixas;
    }

    /// <summary>"+7", "+1 ou +2", "+1 a +3" ou "não evolui".</summary>
    public static string Descrever(int minimo, int maximo) =>
        maximo <= 0 ? "não evolui"
        : minimo == maximo ? $"+{minimo}"
        : maximo == minimo + 1 ? $"+{minimo} ou +{maximo}"
        : $"+{minimo} a +{maximo}";
}
