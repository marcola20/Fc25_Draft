namespace Fc25Draft.Core.Entities;

public enum AdminActionType
{
    None = 0,
    AdjustBudget = 1,
    CancelMarketItem = 2,
    SellPlayers = 3,
    SwapPlayers = 4,
    MovePlayer = 5,
    ResetMarketItemBids = 6,
    CancelOffer = 7,
    LancarAlbum = 8,
    DarPacotes = 9,
    AjustarLendarias = 10,
    AjustarPontosBolao = 11,
    ApagarPerfilTreinador = 12,
    ContarJogosAntesDoLancamento = 13,
    ColocarLenda = 14,
    TirarLenda = 15,
    AtualizarAlbum = 16,

    /// <summary>Colocou, mudou, tirou ou trocou a arte de figurinha das páginas especiais (Organização, Campeões, Lendários).</summary>
    FigurinhasEspeciais = 17
}
