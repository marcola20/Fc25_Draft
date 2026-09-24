namespace Fc25Draft.Core.Enums;

public enum DraftAutoPickModo
{
    /// <summary>Uma lista única de jogadores; na vez do time, pega o primeiro ainda disponível.</summary>
    Jogador = 0,

    /// <summary>Cada rodada tem uma posição e cada posição tem sua lista de prioridade.</summary>
    Posicao = 1
}
