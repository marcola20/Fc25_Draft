using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Quem estava no comando do time em cada jogo: o jogo é de quem estava lá quando ele começou.
/// A passagem guarda só o dia; o horário vem da saída registrada (<see cref="TreinadorPassagem.SaiuEm"/>),
/// que também marca a chegada de quem assumiu no mesmo dia. Assim quem entra de manhã, comanda o jogo
/// e sai à noite fica com ele. Sem esse horário (passagens antigas, saída anotada depois), vale a regra
/// do dia: quem sai leva o jogo do dia da saída e quem chega assume depois dele.
/// Cada partida continua com um dono só, e a soma dos treinadores fecha com o clube.
/// </summary>
public static class ComandoDoJogo
{
    public sealed record Periodo(Guid PassagemId, Guid TimeId, PapelTreinador Papel, DateTime Desde, DateTime? Ate, DateTime? SaiuEm);

    /// <summary>Do momento (UTC) em que a pessoa assumiu até quando saiu (nulo = ainda está lá).</summary>
    public static (DateTime Inicio, DateTime? Fim) Janela(Periodo passagem, IEnumerable<Periodo> doMesmoClube)
    {
        var fim = Fim(passagem);

        // Assumiu no dia em que o anterior saiu: começa na hora em que ele saiu. Com vários no mesmo dia,
        // o anterior é o último que saiu antes desta passagem acabar.
        var saidaDoAnterior = doMesmoClube
            .Where(o => o.PassagemId != passagem.PassagemId && o.TimeId == passagem.TimeId && o.Papel == passagem.Papel
                        && o.Ate?.Date == passagem.Desde.Date)
            .Select(o => Fim(o)!.Value)
            .Where(f => fim is null || f < fim)
            .OrderDescending()
            .Cast<DateTime?>()
            .FirstOrDefault();

        return (saidaDoAnterior ?? FimDoDia(passagem.Desde), fim);
    }

    public static bool Comandou((DateTime Inicio, DateTime? Fim) janela, DateTime comecoDoJogoUtc) =>
        comecoDoJogoUtc > janela.Inicio && (janela.Fim is null || comecoDoJogoUtc <= janela.Fim);

    /// <summary>
    /// A saída registrada vale quando foi no dia anotado como saída. O dia pode ter sido anotado no horário
    /// de Brasília ou no do servidor (UTC), então serve qualquer um dos dois.
    /// </summary>
    private static DateTime? Fim(Periodo p)
    {
        if (p.Ate is not DateTime ate) return null;

        if (p.SaiuEm is DateTime saiu)
        {
            var utc = DateTime.SpecifyKind(saiu, DateTimeKind.Utc);
            if (utc.Date == ate.Date || TimeZoneInfo.ConvertTimeFromUtc(utc, HorarioDeBrasilia.Fuso).Date == ate.Date)
                return utc;
        }

        return FimDoDia(ate);
    }

    /// <summary>Meia-noite do dia seguinte em Brasília, em UTC.</summary>
    private static DateTime FimDoDia(DateTime dia) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dia.Date.AddDays(1), DateTimeKind.Unspecified), HorarioDeBrasilia.Fuso);
}
