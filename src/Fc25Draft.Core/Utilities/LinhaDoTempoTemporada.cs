using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>Um fato marcante da temporada: em que rodada, o texto e, quando há, o time e o jogo.</summary>
public sealed record FatoDaTemporada(int Rodada, string Icone, string Texto, string? TimeNome = null, LigaPartidaDto? Jogo = null);

/// <summary>
/// Conta a história da fase de pontos da Liga a partir dos placares e da posição em cada rodada:
/// trocas de líder, o craque de cada rodada, recordes de goleada, sequências, quem entra nas zonas na reta
/// final e as maiores arrancadas. Os fatos saem na ordem das rodadas.
/// </summary>
public static class LinhaDoTempoTemporada
{
    private const int MinInvicto = 5, MinVitorias = 4, MinJejum = 5, MinGoleada = 3, MinGolsNoJogo = 6, MinArrancada = 4;

    public static IReadOnlyList<FatoDaTemporada> Gerar(
        IEnumerable<LigaRodadaComPartidasDto> rodadas,
        IReadOnlyList<LigaClassificacaoItemDto> tabela,
        IReadOnlyDictionary<Guid, IReadOnlyList<PosicaoNaRodada>> posicoes,
        LigaRegraZonas regra,
        IReadOnlyDictionary<Guid, IReadOnlyList<SelecaoRodadaJogadorDto>>? selecoes = null)
    {
        var regulares = rodadas.Where(r => r.Numero > 0 && !r.Desempate).OrderBy(r => r.Numero).ToList();
        var numeros = posicoes.Values.FirstOrDefault()?.Select(p => p.Rodada).ToList() ?? [];
        if (numeros.Count == 0 || tabela.Count == 0) return [];

        var nomes = tabela.ToDictionary(t => t.TimeId, t => t.TimeNome);
        var faseEncerrada = regulares.SelectMany(r => r.Partidas).All(p => p.Status == PartidaStatus.Encerrada);
        var fatos = new List<FatoDaTemporada>();

        Liderancas(fatos, tabela, posicoes, numeros, faseEncerrada);
        CraquesDasRodadas(fatos, regulares, selecoes);
        Recordes(fatos, regulares, numeros[^1]);
        Sequencias(fatos, regulares, nomes, numeros[^1], faseEncerrada);
        Zonas(fatos, tabela, posicoes, numeros, regulares.Count, regra);
        Arrancadas(fatos, tabela, posicoes, regulares.Count);

        // Na mesma rodada: liderança primeiro, depois o resto na ordem em que foi gerado.
        return fatos
            .Select((f, i) => (f, i))
            .OrderBy(x => x.f.Rodada)
            .ThenBy(x => x.f.Icone == "👑" ? 0 : 1)
            .ThenBy(x => x.i)
            .Select(x => x.f)
            .ToList();
    }

    private static void Liderancas(
        List<FatoDaTemporada> fatos, IReadOnlyList<LigaClassificacaoItemDto> tabela,
        IReadOnlyDictionary<Guid, IReadOnlyList<PosicaoNaRodada>> posicoes, List<int> numeros, bool faseEncerrada)
    {
        Guid? anterior = null;
        var ultimaTroca = -1;
        for (var i = 0; i < numeros.Count; i++)
        {
            // Posição dividida (Série B): fica com o primeiro na ordem da tabela.
            var lider = tabela.FirstOrDefault(t => posicoes[t.TimeId][i].Posicao == 1);
            if (lider is null || lider.TimeId == anterior) continue;

            fatos.Add(new FatoDaTemporada(numeros[i], "👑",
                anterior is null ? $"{lider.TimeNome} é o primeiro líder" : $"{lider.TimeNome} assume a liderança",
                lider.TimeNome));
            anterior = lider.TimeId;
            ultimaTroca = fatos.Count - 1;
        }

        if (ultimaTroca >= 0 && fatos[ultimaTroca].Rodada < numeros[^1])
        {
            var f = fatos[ultimaTroca];
            fatos[ultimaTroca] = f with { Texto = f.Texto + (faseEncerrada ? " e não sai mais" : " e segue lá até agora") };
        }
    }

    /// <summary>
    /// O craque de cada rodada: entre os melhores em campo dos jogos dela, o de maior nota do PES (sem a marca,
    /// a maior nota da seleção da rodada). O fato abre o jogo em que ele jogou.
    /// </summary>
    private static void CraquesDasRodadas(
        List<FatoDaTemporada> fatos, List<LigaRodadaComPartidasDto> regulares,
        IReadOnlyDictionary<Guid, IReadOnlyList<SelecaoRodadaJogadorDto>>? selecoes)
    {
        if (selecoes is null) return;
        var br = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

        foreach (var rodada in regulares)
        {
            if (!selecoes.TryGetValue(rodada.RodadaId, out var selecao) || selecao.Count == 0) continue;
            var craque = selecao.Where(j => j.MelhorEmCampo).OrderByDescending(j => j.Nota).FirstOrDefault()
                         ?? selecao.OrderByDescending(j => j.Nota).First();
            var jogo = rodada.Partidas.FirstOrDefault(p => p.Status == PartidaStatus.Encerrada
                                                           && (p.TimeCasaId == craque.TimeId || p.TimeForaId == craque.TimeId));
            fatos.Add(new FatoDaTemporada(rodada.Numero, "⭐",
                $"Craque da rodada: {craque.JogadorNome} ({craque.TimeNome}), nota {craque.Nota.ToString("0.0", br)}",
                craque.TimeNome, jogo));
        }
    }

    private static void Recordes(List<FatoDaTemporada> fatos, List<LigaRodadaComPartidasDto> regulares, int ultima)
    {
        var maiorDiferenca = MinGoleada - 1;
        var maisGols = MinGolsNoJogo - 1;
        foreach (var rodada in regulares.Where(r => r.Numero <= ultima))
        {
            foreach (var p in rodada.Partidas
                         .Where(p => p.Status == PartidaStatus.Encerrada && !p.IsWO)
                         .OrderBy(p => p.EncerradaEm ?? DateTime.MaxValue))
            {
                var diferenca = Math.Abs(p.GolsCasa - p.GolsFora);
                var gols = p.GolsCasa + p.GolsFora;
                var placar = $"{p.TimeCasaNome} {p.GolsCasa}-{p.GolsFora} {p.TimeForaNome}";
                var vencedor = p.GolsCasa > p.GolsFora ? p.TimeCasaNome : p.TimeForaNome;

                if (diferenca > maiorDiferenca)
                {
                    maiorDiferenca = diferenca;
                    fatos.Add(new FatoDaTemporada(rodada.Numero, "💥", $"Maior goleada até aqui: {placar}", vencedor, p));
                    if (gols > maisGols) maisGols = gols;
                }
                else if (gols > maisGols)
                {
                    maisGols = gols;
                    fatos.Add(new FatoDaTemporada(rodada.Numero, "⚽", $"Jogo com mais gols até aqui: {placar}", null, p));
                }
            }
        }
    }

    private sealed class Sequencia
    {
        public int Invicto, Vitorias, SemVencer, Jogos;
        public bool PerdeuAlguma;
    }

    private static void Sequencias(
        List<FatoDaTemporada> fatos, List<LigaRodadaComPartidasDto> regulares, Dictionary<Guid, string> nomes,
        int ultima, bool faseEncerrada)
    {
        var seq = nomes.Keys.ToDictionary(id => id, _ => new Sequencia());

        foreach (var rodada in regulares.Where(r => r.Numero <= ultima))
        {
            foreach (var p in rodada.Partidas.Where(p => p.Status == PartidaStatus.Encerrada))
            {
                Anotar(p.TimeCasaId, p.TimeForaId, p.GolsCasa, p.GolsFora, rodada.Numero, p);
                Anotar(p.TimeForaId, p.TimeCasaId, p.GolsFora, p.GolsCasa, rodada.Numero, p);
            }
        }

        // O que segue valendo na última rodada disputada.
        foreach (var (id, s) in seq)
        {
            var nome = nomes[id];
            if (!s.PerdeuAlguma && s.Jogos >= MinInvicto)
                fatos.Add(new FatoDaTemporada(ultima, "🛡️",
                    faseEncerrada ? $"{nome} termina a fase invicto ({s.Jogos} jogos)" : $"{nome} segue invicto na temporada ({s.Jogos} jogos)", nome));
            else if (s.Vitorias >= MinVitorias)
                fatos.Add(new FatoDaTemporada(ultima, "🔥", $"{nome} chega a {s.Vitorias} vitórias seguidas", nome));
            else if (s.Invicto >= MinInvicto)
                fatos.Add(new FatoDaTemporada(ultima, "🛡️", $"{nome} está há {s.Invicto} jogos sem perder", nome));
            else if (s.SemVencer >= MinJejum)
                fatos.Add(new FatoDaTemporada(ultima, "😬", $"{nome} está há {s.SemVencer} jogos sem vencer", nome));
        }

        void Anotar(Guid time, Guid adversario, int pro, int contra, int rodada, LigaPartidaDto jogo)
        {
            if (!seq.TryGetValue(time, out var s) || !nomes.TryGetValue(adversario, out var adv)) return;
            var nome = nomes[time];
            s.Jogos++;

            if (pro > contra)
            {
                if (s.SemVencer >= MinJejum)
                    fatos.Add(new FatoDaTemporada(rodada, "😮‍💨", $"{nome} vence o {adv} e quebra jejum de {s.SemVencer} jogos", nome, jogo));
                s.Vitorias++;
                s.Invicto++;
                s.SemVencer = 0;
                return;
            }

            // Uma história por jogo: a invencibilidade longa vale mais que a série de vitórias.
            if (pro < contra && s.Invicto >= MinInvicto)
                fatos.Add(new FatoDaTemporada(rodada, "🛑", $"{nome} perde para o {adv} e encerra {s.Invicto} jogos de invencibilidade", nome, jogo));
            else if (s.Vitorias >= MinVitorias)
                fatos.Add(new FatoDaTemporada(rodada, "✋", $"{nome} tinha {s.Vitorias} vitórias seguidas e parou no {adv}", nome, jogo));

            s.Vitorias = 0;
            s.SemVencer++;
            if (pro < contra)
            {
                s.Invicto = 0;
                s.PerdeuAlguma = true;
            }
            else
            {
                s.Invicto++;
            }
        }
    }

    private static void Zonas(
        List<FatoDaTemporada> fatos, IReadOnlyList<LigaClassificacaoItemDto> tabela,
        IReadOnlyDictionary<Guid, IReadOnlyList<PosicaoNaRodada>> posicoes, List<int> numeros, int totalRodadas,
        LigaRegraZonas regra)
    {
        // Só na reta final (último terço): no começo a tabela muda demais para virar notícia.
        var inicio = totalRodadas - totalRodadas / 3;
        var total = tabela.Count;

        for (var i = 1; i < numeros.Count; i++)
        {
            if (numeros[i] <= inicio) continue;
            foreach (var t in tabela)
            {
                // Só quem entra: quem saiu fica implícito e a rodada não vira uma lista de troca-troca.
                var antes = Relevante(LigaZonas.Zona(regra, posicoes[t.TimeId][i - 1].Posicao, total));
                var agora = Relevante(LigaZonas.Zona(regra, posicoes[t.TimeId][i].Posicao, total));
                if (agora is null || antes == agora) continue;

                fatos.Add(new FatoDaTemporada(numeros[i], agora == "rebaixamento" ? "⬇️" : "⬆️",
                    $"{t.TimeNome} entra na zona de {agora} ({posicoes[t.TimeId][i].Posicao}º)", t.TimeNome));
            }
        }

        static string? Relevante(ZonaClassificacao z) => z switch
        {
            ZonaClassificacao.Rebaixamento => "rebaixamento",
            ZonaClassificacao.AcessoDireto or ZonaClassificacao.CampeaoComAcesso => "acesso",
            _ => null
        };
    }

    private static void Arrancadas(
        List<FatoDaTemporada> fatos, IReadOnlyList<LigaClassificacaoItemDto> tabela,
        IReadOnlyDictionary<Guid, IReadOnlyList<PosicaoNaRodada>> posicoes, int totalRodadas)
    {
        // No primeiro quarto a tabela pula demais (poucos pontos separam todo mundo).
        var aPartirDe = Math.Max(3, totalRodadas / 4);
        (LigaClassificacaoItemDto Time, PosicaoNaRodada De, PosicaoNaRodada Para, int Salto)? subida = null, queda = null;

        foreach (var t in tabela)
        {
            var serie = posicoes[t.TimeId];
            for (var i = 1; i < serie.Count; i++)
            {
                if (serie[i].Rodada <= aPartirDe) continue;
                var salto = serie[i - 1].Posicao - serie[i].Posicao;
                if (salto >= MinArrancada && salto > (subida?.Salto ?? 0))
                    subida = (t, serie[i - 1], serie[i], salto);
                if (-salto >= MinArrancada && -salto > (queda?.Salto ?? 0))
                    queda = (t, serie[i - 1], serie[i], -salto);
            }
        }

        if (subida is { } s)
            fatos.Add(new FatoDaTemporada(s.Para.Rodada, "🚀",
                $"Maior arrancada da temporada: {s.Time.TimeNome} sobe {s.Salto} posições numa rodada ({s.De.Posicao}º → {s.Para.Posicao}º)", s.Time.TimeNome));
        if (queda is { } q)
            fatos.Add(new FatoDaTemporada(q.Para.Rodada, "📉",
                $"Maior tombo da temporada: {q.Time.TimeNome} cai {q.Salto} posições numa rodada ({q.De.Posicao}º → {q.Para.Posicao}º)", q.Time.TimeNome));
    }
}
