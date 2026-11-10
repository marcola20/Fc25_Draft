using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>Recorte da classificação: todos os jogos, só os em casa ou só os fora.</summary>
public enum Mando { Geral, Casa, Fora }

/// <summary>
/// Classificação contando só os jogos em casa (ou só os fora). Usa as mesmas partidas do
/// recálculo oficial — rodadas regulares, sem jogo decisivo, tudo que não está agendado —
/// mas sem punições, que valem para a temporada e não para um mando.
/// </summary>
public static class TabelaPorMando
{
    public static IReadOnlyList<LigaClassificacaoItemDto> Calcular(
        IEnumerable<LigaRodadaComPartidasDto> rodadas,
        IEnumerable<LigaClassificacaoItemDto> times,
        Mando mando)
    {
        var linhas = times.ToDictionary(t => t.TimeId, t => new Linha(t.TimeId, t.TimeNome, t.Grupo));

        var partidas = rodadas
            .Where(r => r.Numero > 0 && !r.Desempate && r.Titulo is null)
            .SelectMany(r => r.Partidas)
            .Where(p => p.Status != PartidaStatus.Agendada);

        foreach (var p in partidas)
        {
            if (mando != Mando.Fora && linhas.TryGetValue(p.TimeCasaId, out var casa))
                casa.Somar(p.GolsCasa, p.GolsFora);
            if (mando != Mando.Casa && linhas.TryGetValue(p.TimeForaId, out var fora))
                fora.Somar(p.GolsFora, p.GolsCasa);
        }

        var ordenados = LigaDesempate.Ordenar(
                linhas.Values.OrderBy(l => l.Nome, StringComparer.CurrentCulture),
                TipoCompetition.Liga,
                l => l.TimeId,
                l => new DesempateStats(l.Pontos, l.V, l.GP - l.GC, l.GP),
                Array.Empty<ConfrontoDireto>());

        // Iguais em Pontos, Vitórias, Saldo e Gols Pró dividem a posição.
        var resultado = new List<LigaClassificacaoItemDto>(ordenados.Count);
        for (int i = 0; i < ordenados.Count; i++)
        {
            var l = ordenados[i];
            var posicao = i > 0 && MesmosNumeros(l, ordenados[i - 1]) ? resultado[i - 1].Posicao : i + 1;
            resultado.Add(new LigaClassificacaoItemDto(
                posicao, l.TimeId, l.Nome, l.Pontos, l.J, l.V, l.E, l.D, l.GP, l.GC, l.GP - l.GC,
                0, 0, 0, l.Grupo));
        }
        return resultado;

        static bool MesmosNumeros(Linha a, Linha b) =>
            a.Pontos == b.Pontos && a.V == b.V && a.GP - a.GC == b.GP - b.GC && a.GP == b.GP;
    }

    /// <summary>A partida entra no recorte para este time (ele jogou em casa / fora)?</summary>
    public static bool Conta(LigaPartidaDto p, Guid timeId, Mando mando) => mando switch
    {
        Mando.Casa => p.TimeCasaId == timeId,
        Mando.Fora => p.TimeForaId == timeId,
        _ => p.TimeCasaId == timeId || p.TimeForaId == timeId
    };

    private sealed class Linha(Guid timeId, string nome, GrupoCopa? grupo)
    {
        public Guid TimeId { get; } = timeId;
        public string Nome { get; } = nome;
        public GrupoCopa? Grupo { get; } = grupo;
        public int J, V, E, D, GP, GC;
        public int Pontos => V * 3 + E;

        public void Somar(int pro, int contra)
        {
            J++;
            GP += pro;
            GC += contra;
            if (pro > contra) V++;
            else if (pro < contra) D++;
            else E++;
        }
    }
}
