using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Regras dos prêmios de destaque (as mesmas do formulário que a liga usava): quantos votos cada pessoa dá na
/// categoria, quantos vencedores ela tem e se o prêmio vale +1 over.
/// </summary>
public static class PremiosTemporada
{
    public record Regra(
        CategoriaPremio Categoria, string Emoji, string Sigla, string Nome, string Pergunta,
        int Votos, int Vencedores, bool DaOver, bool DeTreinador = false);

    /// <summary>ROY: jogador que entrou na liga na temporada e tem até este overall.</summary>
    public const int OverallMaximoRoy = 85;

    public static readonly IReadOnlyList<Regra> Todas =
    [
        new(CategoriaPremio.MVP, "⭐", "MVP", "Melhor Jogador da Temporada", "Quem foi o melhor jogador?", 1, 1, true),
        new(CategoriaPremio.ROY, "🌟", "ROY", "Melhor Jogador Novo",
            $"Melhor jogador novo na temporada? (até {OverallMaximoRoy} de overall, entrou nesta temporada)", 3, 3, true),
        new(CategoriaPremio.MIP, "📈", "MIP", "Jogador que Mais Evoluiu", "Quem foi o jogador que mais evoluiu?", 2, 3, true),
        new(CategoriaPremio.DPOY, "🛡️", "DPOY", "Melhor Defensor", "Quem foi o melhor defensor? (zagueiros e laterais)", 4, 1, true),
        new(CategoriaPremio.GKOY, "🧤", "GKOY", "Melhor Goleiro", "Quem foi o melhor goleiro?", 2, 1, true),
        new(CategoriaPremio.COTY, "🧠", "COTY", "Melhor Treinador", "Quem foi o melhor treinador?", 1, 1, false, DeTreinador: true),
    ];

    public static Regra De(CategoriaPremio categoria) => Todas.First(r => r.Categoria == categoria);

    /// <summary>"MVP 2010", "ROY 2010".</summary>
    public static string Titulo(CategoriaPremio categoria, int temporada) => $"{De(categoria).Sigla} {temporada}";
}
