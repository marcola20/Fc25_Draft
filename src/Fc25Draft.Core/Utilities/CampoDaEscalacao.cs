namespace Fc25Draft.Core.Utilities;

/// <summary>Onde um titular aparece no campo desenhado: em % da largura (X) e da altura (Y) do campo inteiro.</summary>
public sealed record LugarNoCampo(int JogadorId, double X, double Y);

/// <summary>
/// Põe os titulares de cada time no campo, como na TV: mandante na metade de baixo (goleiro embaixo) e visitante
/// na de cima (goleiro em cima, espelhado). A posição sai do código do lugar na escalação (GK, LCB, CDM1, LW...);
/// sem ele (jogo antigo, ou titular que veio das notas do PES fora da escalação), da posição natural do jogador.
/// <para>
/// Cada lugar tem uma altura (do gol ao ataque) e um corredor. Os abertos (laterais, meias pelos lados e pontas)
/// ficam colados na lateral; os centrais da mesma altura se espalham pelo meio, da esquerda para a direita (os
/// números do código: CDM1, CDM2...). O meia de ligação (CM) muda de altura conforme a formação: sem volante ele
/// é o meio-campo; com um volante só e um CM só, fica ao lado dele; senão, um pouco à frente dos volantes (no
/// losango VOL–2 MLG–MAT, um de cada lado).
/// </para>
/// </summary>
public static class CampoDaEscalacao
{
    // Alturas, do gol (0) ao ataque (Ataque).
    private const double Goleiro = 0, Defesa = 1, Alas = 1.25, Volantes = 2, MeiaPeloLado = 2.3,
        MeioAFrente = 2.6, Meias = 3.3, SegundoAtacante = 3.8, Pontas = 4, Ataque = 4.4;

    // Faixa de cada metade: do goleiro (perto do gol) até a linha mais avançada (perto do meio-campo).
    private const double GolY = 95, MeioY = 54;

    private enum Corredor { Centro, Esquerda, Direita }

    /// <summary>Altura, corredor e ordem da esquerda para a direita entre os centrais da mesma altura.</summary>
    private readonly record struct Lugar(double Altura, Corredor Corredor, double Ordem);

    public static IReadOnlyList<LugarNoCampo> Posicionar(
        IEnumerable<(int JogadorId, string? SlotCode, int PositionId)> titulares, bool mandante)
    {
        var lista = titulares
            .Select(t => (t.JogadorId, Codigo: Separar(t.SlotCode) is { } c && PeloCodigo(c, MeioAFrente) is not null
                ? c
                : Separar(CodigoDaPosicao(t.PositionId))!.Value))
            .ToList();

        var volantes = lista.Count(l => l.Codigo.Base is "CDM" or "VOL");
        var meiasDeLigacao = lista.Count(l => l.Codigo.Base is "CM" or "MLG");
        var alturaDoCm = volantes == 0 ? MeiaPeloLado
            : volantes == 1 && meiasDeLigacao == 1 ? Volantes
            : MeioAFrente;

        var lugares = lista
            .Select(l => (l.JogadorId, l.Codigo, Lugar: PeloCodigo(l.Codigo, alturaDoCm) ?? new Lugar(MeiaPeloLado, Corredor.Centro, 0)))
            .ToList();

        var temCentroavante = lista.Any(l => l.Codigo.Base is "ST" or "CA");
        var temPontaEsquerda = lista.Any(l => l.Codigo.Base == "LW");
        var temPontaDireita = lista.Any(l => l.Codigo.Base == "RW");
        var resultado = new List<LugarNoCampo>(lugares.Count);

        // Centrais: espalhados pelo meio, por altura.
        foreach (var altura in lugares.Where(l => l.Lugar.Corredor == Corredor.Centro).GroupBy(l => l.Lugar.Altura))
        {
            var naAltura = altura
                .OrderBy(l => l.Lugar.Ordem)
                .ThenBy(l => OrdemDoTipo(l.Codigo.Base))
                .ThenBy(l => l.JogadorId)
                .ToList();
            var xs = Espalhar(naAltura.Count);
            // Segundo atacante sozinho atrás de um centroavante: um pouco para o lado (o que não tem ponta), senão o
            // meia, ele e o centroavante ficam empilhados no meio.
            if (naAltura is [{ Codigo.Base: "SA" }] && temCentroavante)
                xs = new double[] { temPontaEsquerda && !temPontaDireita ? 62 : 38 };
            for (var i = 0; i < naAltura.Count; i++)
                resultado.Add(Ponto(naAltura[i].JogadorId, xs[i], altura.Key, mandante));
        }

        // Abertos: colados na lateral. Dois no mesmo canto (só acontece sem código) vão chegando para o meio.
        foreach (var canto in lugares.Where(l => l.Lugar.Corredor != Corredor.Centro).GroupBy(l => (l.Lugar.Altura, l.Lugar.Corredor)))
        {
            var noCanto = canto.OrderBy(l => l.JogadorId).ToList();
            var x = XDaLateral(canto.Key.Altura);
            for (var i = 0; i < noCanto.Count; i++)
            {
                var deslocado = x + i * 10;
                resultado.Add(Ponto(noCanto[i].JogadorId, canto.Key.Corredor == Corredor.Esquerda ? deslocado : 100 - deslocado,
                    canto.Key.Altura, mandante));
            }
        }

        return resultado;
    }

    /// <summary>Se o código do lugar tem posição no campo desenhado (senão vale a posição natural do jogador).</summary>
    public static bool ConheceOCodigo(string? codigo)
        => Separar(codigo) is { } c && PeloCodigo(c, MeioAFrente) is not null;

    private static LugarNoCampo Ponto(int jogadorId, double x, double altura, bool mandante)
    {
        var y = GolY - altura / Ataque * (GolY - MeioY);
        // O visitante ataca para baixo: a esquerda dele fica à direita de quem olha.
        return mandante ? new LugarNoCampo(jogadorId, x, y) : new LugarNoCampo(jogadorId, 100 - x, 100 - y);
    }

    /// <summary>X de quem está aberto: o lateral mais colado na linha, o ponta um pouco para dentro.</summary>
    private static double XDaLateral(double altura) => altura switch
    {
        <= Alas => 10,
        < Pontas => 12,
        _ => 16
    };

    private static double[] Espalhar(int n) => n switch
    {
        1 => new double[] { 50 },
        2 => new double[] { 35, 65 },
        3 => new double[] { 27, 50, 73 },
        4 => new double[] { 20, 40, 60, 80 },
        _ => Enumerable.Range(1, n).Select(i => 100.0 * i / (n + 1)).ToArray()
    };

    // Na mesma altura e sem número (volante e meia de ligação lado a lado): o volante à esquerda.
    private static int OrdemDoTipo(string baseDoCodigo) => baseDoCodigo is "CDM" or "VOL" ? 0 : 1;

    private readonly record struct Codigo(string Base, int Numero);

    /// <summary>"CDM2" vira (CDM, 2); sem número no fim, 0.</summary>
    private static Codigo? Separar(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        var c = codigo.Trim().ToUpperInvariant();
        var numero = c.Length > 1 && char.IsDigit(c[^1]) ? c[^1] - '0' : 0;
        return new Codigo(numero > 0 ? c[..^1] : c, numero);
    }

    /// <summary>Pelo código do lugar. Os números (CDM1, CAM2...) vão da esquerda para a direita.</summary>
    private static Lugar? PeloCodigo(Codigo codigo, double alturaDoCm)
    {
        var ordem = (double)codigo.Numero;
        return codigo.Base switch
        {
            "GK" => new Lugar(Goleiro, Corredor.Centro, 0),
            "LB" => new Lugar(Defesa, Corredor.Esquerda, 0),
            "ADE" => new Lugar(Alas, Corredor.Esquerda, 0),
            "LCB" => new Lugar(Defesa, Corredor.Centro, -1),
            "CCB" or "CB" => new Lugar(Defesa, Corredor.Centro, ordem),
            "RCB" => new Lugar(Defesa, Corredor.Centro, 1),
            "RB" => new Lugar(Defesa, Corredor.Direita, 0),
            "ADD" => new Lugar(Alas, Corredor.Direita, 0),
            "CDM" or "VOL" => new Lugar(Volantes, Corredor.Centro, ordem),
            "LM" => new Lugar(MeiaPeloLado, Corredor.Esquerda, 0),
            "CM" or "MLG" => new Lugar(alturaDoCm, Corredor.Centro, ordem),
            "RM" => new Lugar(MeiaPeloLado, Corredor.Direita, 0),
            "CAM" or "MAT" => new Lugar(Meias, Corredor.Centro, ordem),
            "SA" => new Lugar(SegundoAtacante, Corredor.Centro, ordem),
            "LW" => new Lugar(Pontas, Corredor.Esquerda, 0),
            "ST" or "CA" => new Lugar(Ataque, Corredor.Centro, ordem),
            "RW" => new Lugar(Pontas, Corredor.Direita, 0),
            _ => null
        };
    }

    /// <summary>O código equivalente à posição natural do jogador (ids do PES: 1 GOL ... 13 SA).</summary>
    private static string CodigoDaPosicao(int positionId) => positionId switch
    {
        1 => "GK",
        2 => "CB",
        3 => "LB",
        4 => "RB",
        5 => "CDM",
        6 => "CM",
        7 => "CAM",
        8 => "LM",
        9 => "LW",
        10 => "RM",
        11 => "RW",
        12 => "ST",
        13 => "SA",
        _ => "CM"
    };
}
