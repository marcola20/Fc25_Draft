using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

public static class LigaLabels
{
    public static string DivisaoNome(Divisao divisao) => divisao switch
    {
        Divisao.SerieA => "Série A",
        Divisao.SerieB => "Série B",
        _ => divisao.ToString()
    };

    /// <summary>Rótulo curto da competição: "Série A", "Série B", "Liga" (sem divisão), "Copa" ou "Supercopa".</summary>
    public static string Competicao(TipoCompetition tipo, Divisao? divisao) => tipo switch
    {
        TipoCompetition.Liga => divisao is null ? "Liga" : DivisaoNome(divisao.Value),
        TipoCompetition.Copa => "Copa",
        TipoCompetition.Supercopa => "Supercopa",
        _ => tipo.ToString()
    };

    /// <summary>Status da competição em linguagem de torcedor (na Copa, os nomes das fases mudam).</summary>
    public static string Status(LigaStatus status, TipoCompetition tipo) => status switch
    {
        LigaStatus.Criada => "Criada",
        LigaStatus.PrimeiraFase => tipo == TipoCompetition.Copa ? "Fase de Grupos" : "Primeira Fase",
        // Na Copa o status interno PlayIn corresponde ao mata-mata (semifinais e final).
        LigaStatus.PlayIn => tipo == TipoCompetition.Copa ? "Mata-Mata" : "Play-In",
        LigaStatus.Playoffs => "Playoffs",
        LigaStatus.Encerrada => "Encerrada",
        LigaStatus.DecisaoCampeao => "Decisão de campeão",
        LigaStatus.MiniLiga => "Mini liga do título",
        _ => status.ToString()
    };
}
