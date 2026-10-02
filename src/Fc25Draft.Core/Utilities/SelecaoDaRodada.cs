using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>Nota de um jogador num jogo, com o que a seleção da rodada precisa para escolher.</summary>
public sealed record NotaDoJogo(int JogadorId, string Nome, int PositionId, Guid TimeId, string TimeNome, decimal Nota, bool MelhorEmCampo);

/// <summary>
/// Seleção da rodada em 4-3-3 pelas notas do PES, pela posição do cadastro: goleiro, lateral-direito,
/// 2 zagueiros, lateral-esquerdo, 3 do meio e 3 atacantes, sempre os de maior nota. Vaga sem gente da
/// posição fica com o melhor que sobrou do mesmo setor e, se nem assim, com o melhor de linha
/// (goleiro só entra no gol).
/// </summary>
public static class SelecaoDaRodada
{
    public enum Setor { Goleiro, Defesa, Meio, Ataque }

    private sealed record Vaga(Setor Setor, Func<int, bool> Aceita);

    private static readonly Vaga[] Formacao =
    [
        new(Setor.Goleiro, p => p == (int)PositionType.Goleiro),
        new(Setor.Defesa, p => p == (int)PositionType.LateralDireito),
        new(Setor.Defesa, p => p == (int)PositionType.Zagueiro),
        new(Setor.Defesa, p => p == (int)PositionType.Zagueiro),
        new(Setor.Defesa, p => p == (int)PositionType.LateralEsquerdo),
        new(Setor.Meio, p => SetorDa(p) == Setor.Meio),
        new(Setor.Meio, p => SetorDa(p) == Setor.Meio),
        new(Setor.Meio, p => SetorDa(p) == Setor.Meio),
        new(Setor.Ataque, p => SetorDa(p) == Setor.Ataque),
        new(Setor.Ataque, p => SetorDa(p) == Setor.Ataque),
        new(Setor.Ataque, p => SetorDa(p) == Setor.Ataque),
    ];

    public static Setor SetorDa(int positionId) => (PositionType)positionId switch
    {
        PositionType.Goleiro => Setor.Goleiro,
        PositionType.Zagueiro or PositionType.LateralEsquerdo or PositionType.LateralDireito => Setor.Defesa,
        PositionType.PontaEsquerda or PositionType.PontaDireita or PositionType.Centroavante or PositionType.SegundoAtacante => Setor.Ataque,
        _ => Setor.Meio
    };

    public static IReadOnlyList<NotaDoJogo> Montar(IEnumerable<NotaDoJogo> notas)
    {
        // Melhor nota de cada jogador; desempate: melhor em campo e depois o nome.
        var disponiveis = notas
            .GroupBy(n => n.JogadorId)
            .Select(g => g.OrderByDescending(n => n.Nota).ThenByDescending(n => n.MelhorEmCampo).First())
            .OrderByDescending(n => n.Nota)
            .ThenByDescending(n => n.MelhorEmCampo)
            .ThenBy(n => n.Nome)
            .ToList();

        var escolhidos = new NotaDoJogo?[Formacao.Length];
        bool Livre(NotaDoJogo n) => !escolhidos.Contains(n);

        // 1º a posição certa; 2º o mesmo setor; 3º qualquer jogador de linha.
        var criterios = new Func<Vaga, NotaDoJogo, bool>[]
        {
            (v, n) => v.Aceita(n.PositionId),
            (v, n) => SetorDa(n.PositionId) == v.Setor,
            (v, n) => v.Setor != Setor.Goleiro && SetorDa(n.PositionId) != Setor.Goleiro,
        };
        foreach (var criterio in criterios)
            for (int i = 0; i < Formacao.Length; i++)
                escolhidos[i] ??= disponiveis.FirstOrDefault(n => Livre(n) && criterio(Formacao[i], n));

        return escolhidos.Where(n => n is not null).Select(n => n!).ToList();
    }
}
