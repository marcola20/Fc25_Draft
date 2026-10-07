using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Até onde cada time chegou numa competição de mata-mata (Copa, Supercopa). Quem ainda vai jogar
/// uma fase já conta como tendo chegado nela.
/// </summary>
public static class FasesDoMataMata
{
    public static Dictionary<Guid, FasePremiacao> Calcular(
        IEnumerable<JogoKnockoutInput> jogos, Guid? campeaoGravado, IEnumerable<Guid> participantes)
    {
        var lista = jogos.ToList();
        var fases = new Dictionary<Guid, FasePremiacao>();

        void Marcar(Guid? timeId, FasePremiacao fase)
        {
            if (timeId is not Guid id || id == Guid.Empty) return;
            // A fase mais longe vale: quem perdeu a final não volta a ser "eliminado nas quartas".
            if (!fases.TryGetValue(id, out var atual) || fase < atual) fases[id] = fase;
        }

        foreach (var jogo in lista)
        {
            var ateOndeChegou = jogo.Fase switch
            {
                FaseKnockout.Final => FasePremiacao.Vice,
                FaseKnockout.Semi1 or FaseKnockout.Semi2 => FasePremiacao.Semifinal,
                FaseKnockout.QF1 or FaseKnockout.QF2 or FaseKnockout.QF3 or FaseKnockout.QF4 => FasePremiacao.Quartas,
                _ => FasePremiacao.FaseDeGrupos
            };

            Marcar(jogo.TimeCasaId, ateOndeChegou);
            Marcar(jogo.TimeForaId, ateOndeChegou);

            if (jogo.Fase == FaseKnockout.Final && jogo.VencedorId is Guid campeaoDaFinal)
                fases[campeaoDaFinal] = FasePremiacao.Campeao;
        }

        if (campeaoGravado is Guid campeao)
            fases[campeao] = FasePremiacao.Campeao;

        foreach (var timeId in participantes)
        {
            if (fases.ContainsKey(timeId)) continue;

            // Sem bracket (Supercopa e afins) o outro time do jogo único é o vice;
            // com bracket, quem não apareceu nele parou na fase de grupos.
            fases[timeId] = lista.Count == 0 ? FasePremiacao.Vice : FasePremiacao.FaseDeGrupos;
        }

        return fases;
    }
}
