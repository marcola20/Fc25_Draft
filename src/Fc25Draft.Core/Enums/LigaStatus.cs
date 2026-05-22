namespace Fc25Draft.Core.Enums;

public enum LigaStatus
{
    Criada = 0,
    PrimeiraFase = 1,
    PlayIn = 2,
    Playoffs = 3,
    Encerrada = 4,
    DecisaoCampeao = 5,  // 2 times empatados na liderança (Liga)
    MiniLiga = 6         // 3+ times empatados na liderança (Liga)
}
