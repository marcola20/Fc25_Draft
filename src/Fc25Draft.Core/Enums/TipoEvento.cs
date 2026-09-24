namespace Fc25Draft.Core.Enums;

public enum TipoEvento
{
    None = 0,
    Gol = 1,
    CartaoAmarelo = 2,
    CartaoVermelho = 3,
    GolContra = 4,
    // JogadorId = quem entrou em campo (conta +1 jogo para ele); JogadorSaiuId = quem saiu.
    Substituicao = 5
}
