namespace Fc25Draft.Core.Utilities;

/// <summary>Onde um titular aparece no campo desenhado: em % da largura (X) e da altura (Y) do campo inteiro.</summary>
public sealed record LugarNoCampo(int JogadorId, double X, double Y);

/// <summary>
/// Põe os titulares de cada time no campo, como na TV: mandante na metade de baixo (goleiro embaixo) e visitante
/// na de cima (goleiro em cima, espelhado). A posição sai do código do lugar na escalação (GK, LCB, CDM1, LW...);
/// sem ele (jogo antigo, ou titular que veio das notas do PES fora da escalação), da posição natural do jogador.
/// Cada lugar vira uma linha (goleiro, defesa, volantes, meio, meias, ataque) e um lado; dentro da linha os
/// jogadores se espalham por igual, da esquerda para a direita.
/// </summary>
public static class CampoDaEscalacao
{
    private const int Goleiro = 0, Defesa = 1, Volantes = 2, Meio = 3, Meias = 4, Ataque = 5;

    // Faixa de cada metade: do goleiro (perto do gol) até a linha mais avançada (perto do meio-campo).
    private const double GolY = 95, MeioY = 54;

    public static IReadOnlyList<LugarNoCampo> Posicionar(
        IEnumerable<(int JogadorId, string? SlotCode, int PositionId)> titulares, bool mandante)
    {
        var lugares = titulares
            .Select(t => (t.JogadorId, Lugar: PeloCodigo(t.SlotCode) ?? PelaPosicao(t.PositionId)))
            .ToList();

        var linhas = lugares.Select(l => l.Lugar.Linha).Distinct().OrderBy(l => l).ToList();
        var resultado = new List<LugarNoCampo>();
        foreach (var linha in linhas)
        {
            var naLinha = lugares.Where(l => l.Lugar.Linha == linha)
                .OrderBy(l => l.Lugar.Lado)
                .ThenBy(l => l.JogadorId)
                .ToList();
            var indice = linhas.IndexOf(linha);
            var y = linhas.Count == 1 ? GolY : GolY - indice * (GolY - MeioY) / (linhas.Count - 1);

            for (var i = 0; i < naLinha.Count; i++)
            {
                var x = 100.0 * (i + 1) / (naLinha.Count + 1);
                // O visitante ataca para baixo: a esquerda dele fica à direita de quem olha.
                resultado.Add(mandante
                    ? new LugarNoCampo(naLinha[i].JogadorId, x, y)
                    : new LugarNoCampo(naLinha[i].JogadorId, 100 - x, 100 - y));
            }
        }

        return resultado;
    }

    private readonly record struct Lugar(int Linha, double Lado);

    /// <summary>Pelo código do lugar na escalação. Números (CDM1, CAM2...) vão da esquerda para a direita.</summary>
    private static Lugar? PeloCodigo(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        var c = codigo.Trim().ToUpperInvariant();
        var numero = c.Length > 1 && char.IsDigit(c[^1]) ? c[^1] - '0' : 0;
        var semNumero = numero > 0 ? c[..^1] : c;
        double Numerado(double semNumeroLado) => numero > 0 ? numero - 1.5 : semNumeroLado;

        return semNumero switch
        {
            "GK" => new Lugar(Goleiro, 0),
            "LB" or "ADE" => new Lugar(Defesa, -2),
            "LCB" => new Lugar(Defesa, -1),
            "CCB" or "CB" => new Lugar(Defesa, 0),
            "RCB" => new Lugar(Defesa, 1),
            "RB" or "ADD" => new Lugar(Defesa, 2),
            "CDM" or "VOL" => new Lugar(Volantes, Numerado(0)),
            "LM" => new Lugar(Meio, -2),
            "CM" or "MLG" => new Lugar(Meio, Numerado(0)),
            "RM" => new Lugar(Meio, 2),
            "CAM" or "MAT" => new Lugar(Meias, Numerado(0)),
            "SA" => new Lugar(Meias, Numerado(0)),
            "LW" => new Lugar(Ataque, -2),
            "ST" or "CA" => new Lugar(Ataque, Numerado(0)),
            "RW" => new Lugar(Ataque, 2),
            _ => null
        };
    }

    /// <summary>Pela posição natural do jogador (ids do PES: 1 GOL ... 13 SA).</summary>
    private static Lugar PelaPosicao(int positionId) => positionId switch
    {
        1 => new Lugar(Goleiro, 0),
        2 => new Lugar(Defesa, 0),
        3 => new Lugar(Defesa, -2),
        4 => new Lugar(Defesa, 2),
        5 => new Lugar(Volantes, 0),
        6 => new Lugar(Meio, 0),
        7 => new Lugar(Meias, 0),
        8 => new Lugar(Meio, -2),
        9 => new Lugar(Ataque, -2),
        10 => new Lugar(Meio, 2),
        11 => new Lugar(Ataque, 2),
        12 => new Lugar(Ataque, 0),
        13 => new Lugar(Meias, 0),
        _ => new Lugar(Meio, 0)
    };
}
