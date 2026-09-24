using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

public record JogoKnockoutInput(FaseKnockout Fase, Guid? TimeCasaId, Guid? TimeForaId, Guid? VencedorId);

/// <summary>
/// Classificação final de uma edição de liga. Nas temporadas em que o título saiu do mata-mata,
/// a tabela de pontos corridos é só a fase regular: o campeão é quem venceu a final, e quem caiu
/// na mesma fase é ordenado por quem o eliminou — perder para o campeão vale mais do que perder
/// para o vice, e assim por diante. Sem mata-mata decidido, vale a posição da tabela.
/// </summary>
public static class PosicaoFinalLiga
{
    public static IReadOnlyDictionary<Guid, int> Calcular(
        IReadOnlyDictionary<Guid, int> posicaoNaTabela,
        IEnumerable<JogoKnockoutInput> jogos)
    {
        var decididos = jogos
            .Where(j => j.VencedorId is not null && j.TimeCasaId is not null && j.TimeForaId is not null)
            .ToList();

        var final = decididos.FirstOrDefault(j => j.Fase == FaseKnockout.Final);
        if (final is null) return posicaoNaTabela;

        // Último jogo de cada time no mata-mata: é nele que ele parou.
        var ultimoJogo = new Dictionary<Guid, JogoKnockoutInput>();
        foreach (var jogo in decididos)
            foreach (var timeId in new[] { jogo.TimeCasaId!.Value, jogo.TimeForaId!.Value })
                if (!ultimoJogo.TryGetValue(timeId, out var atual) || Nivel(jogo.Fase) > Nivel(atual.Fase))
                    ultimoJogo[timeId] = jogo;

        var campeao = final.VencedorId!.Value;
        var posicoes = new Dictionary<Guid, int>();
        var proxima = 1;

        int NaTabela(Guid timeId) => posicaoNaTabela.TryGetValue(timeId, out var p) ? p : int.MaxValue;
        int PosicaoDoAlgoz(Guid timeId) =>
            ultimoJogo[timeId].VencedorId is Guid algoz && posicoes.TryGetValue(algoz, out var p) ? p : int.MaxValue;

        // Campeão primeiro; depois, quem foi mais longe. Dentro da mesma fase, quem perdeu
        // para o time melhor colocado fica na frente.
        var porFase = ultimoJogo.Keys
            .Where(id => id != campeao)
            .GroupBy(id => Nivel(ultimoJogo[id].Fase))
            .OrderByDescending(g => g.Key);

        posicoes[campeao] = proxima++;

        foreach (var fase in porFase)
            foreach (var timeId in fase.OrderBy(PosicaoDoAlgoz).ThenBy(NaTabela))
                posicoes[timeId] = proxima++;

        // Quem não entrou no mata-mata segue a ordem da fase regular.
        foreach (var timeId in posicaoNaTabela.Keys.Where(id => !posicoes.ContainsKey(id)).OrderBy(NaTabela))
            posicoes[timeId] = proxima++;

        return posicoes;
    }

    /// <summary>Quão longe a fase fica no chaveamento (os dois jogos de uma mesma fase empatam).</summary>
    private static int Nivel(FaseKnockout fase) => fase switch
    {
        FaseKnockout.Final => 5,
        FaseKnockout.Semi1 or FaseKnockout.Semi2 => 4,
        FaseKnockout.QF1 or FaseKnockout.QF2 or FaseKnockout.QF3 or FaseKnockout.QF4 => 3,
        FaseKnockout.PlayIn_C => 2,
        FaseKnockout.PlayIn_A or FaseKnockout.PlayIn_B => 1,
        _ => 0
    };
}
