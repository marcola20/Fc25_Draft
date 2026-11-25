namespace Fc25Draft.Core.Utilities;

/// <summary>Números que fazem um jogador virar ídolo de um clube.</summary>
public static class IdolosCriterios
{
    public const int Jogos = 50;
    public const int Gols = 30;
    public const int Assistencias = 15;
    public const int ParticipacoesEmGol = 40;

    /// <summary>No título: artilheiro e garçom do time só contam com pelo menos este tanto.</summary>
    public const int MinimoNoTitulo = 3;

    /// <summary>Melhor nota do time no título: precisa ter nota em pelo menos metade dos jogos do time.</summary>
    public const double ParteDosJogosComNota = 0.5;

    /// <summary>Quanto a confiança da diretoria cai quando o clube vende ou troca um ídolo.</summary>
    public const double SaidaNaConfianca = -5;
}

/// <summary>
/// Ídolo de um clube: somando a passagem dele pelo clube, <see cref="IdolosCriterios.Jogos"/> jogos, ou
/// <see cref="IdolosCriterios.Gols"/> gols, ou <see cref="IdolosCriterios.Assistencias"/> assistências, ou
/// <see cref="IdolosCriterios.ParticipacoesEmGol"/> gols + assistências. Ou ter sido destaque de um título do clube
/// (Série A, Série B ou Copa): artilheiro do time, garçom do time ou melhor nota média do time na campanha.
/// </summary>
public static class Idolos
{
    /// <summary>Números de um jogador por um clube numa competição.</summary>
    public sealed record Estatistica(
        int JogadorId, Guid TimeId, Guid LigaId, int Jogos, int Gols, int Assistencias, decimal? NotaMedia, int JogosComNota);

    /// <summary>Título de um clube: a competição e quantos jogos o clube fez nela.</summary>
    public sealed record Titulo(Guid LigaId, string Competicao, Guid CampeaoId, int JogosDoTime);

    public sealed record Resultado(
        int JogadorId, Guid TimeId, int Jogos, int Gols, int Assistencias, int Titulos, IReadOnlyList<string> Motivos);

    /// <summary>Os ídolos de todos os clubes, com o motivo de cada um.</summary>
    public static IReadOnlyList<Resultado> Calcular(IReadOnlyCollection<Estatistica> estatisticas, IEnumerable<Titulo> titulos)
    {
        // Destaques de cada título, por (jogador, clube).
        var destaques = new Dictionary<(int, Guid), List<string>>();
        var titulosDoJogador = new Dictionary<(int, Guid), int>();
        void Destaque(int jogador, Guid time, string motivo)
        {
            if (!destaques.TryGetValue((jogador, time), out var lista)) destaques[(jogador, time)] = lista = new List<string>();
            lista.Add(motivo);
        }

        foreach (var t in titulos)
        {
            var doTime = estatisticas.Where(e => e.LigaId == t.LigaId && e.TimeId == t.CampeaoId).ToList();
            if (doTime.Count == 0) continue;

            foreach (var e in doTime.Where(e => e.Jogos > 0 || e.Gols > 0 || e.Assistencias > 0))
                titulosDoJogador[(e.JogadorId, e.TimeId)] = titulosDoJogador.GetValueOrDefault((e.JogadorId, e.TimeId)) + 1;

            var maisGols = doTime.Max(e => e.Gols);
            if (maisGols >= IdolosCriterios.MinimoNoTitulo)
                foreach (var e in doTime.Where(e => e.Gols == maisGols))
                    Destaque(e.JogadorId, e.TimeId, $"Artilheiro do título · {t.Competicao}");

            var maisAssist = doTime.Max(e => e.Assistencias);
            if (maisAssist >= IdolosCriterios.MinimoNoTitulo)
                foreach (var e in doTime.Where(e => e.Assistencias == maisAssist))
                    Destaque(e.JogadorId, e.TimeId, $"Garçom do título · {t.Competicao}");

            var minimoComNota = Math.Max(1, (int)Math.Ceiling(t.JogosDoTime * IdolosCriterios.ParteDosJogosComNota));
            var comNota = doTime.Where(e => e.NotaMedia is not null && e.JogosComNota >= minimoComNota).ToList();
            if (comNota.Count > 0)
            {
                var melhor = comNota.Max(e => Math.Round(e.NotaMedia!.Value, 2));
                foreach (var e in comNota.Where(e => Math.Round(e.NotaMedia!.Value, 2) == melhor))
                    Destaque(e.JogadorId, e.TimeId, $"Melhor nota do título · {t.Competicao} ({melhor.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))})");
            }
        }

        var resultado = new List<Resultado>();
        foreach (var g in estatisticas.GroupBy(e => (e.JogadorId, e.TimeId)))
        {
            int jogos = g.Sum(e => e.Jogos), gols = g.Sum(e => e.Gols), assist = g.Sum(e => e.Assistencias);
            var motivos = new List<string>();
            if (jogos >= IdolosCriterios.Jogos) motivos.Add($"{jogos} jogos");
            if (gols >= IdolosCriterios.Gols) motivos.Add($"{gols} gols");
            if (assist >= IdolosCriterios.Assistencias) motivos.Add($"{assist} assistências");
            if (gols < IdolosCriterios.Gols && assist < IdolosCriterios.Assistencias && gols + assist >= IdolosCriterios.ParticipacoesEmGol)
                motivos.Add($"{gols + assist} gols + assistências");
            if (destaques.TryGetValue(g.Key, out var doTitulo)) motivos.AddRange(doTitulo);

            if (motivos.Count > 0)
                resultado.Add(new Resultado(g.Key.JogadorId, g.Key.TimeId, jogos, gols, assist,
                    titulosDoJogador.GetValueOrDefault(g.Key), motivos));
        }

        return resultado;
    }
}
