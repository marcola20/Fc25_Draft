using System.Globalization;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Pesos da nota da liga. É o único lugar a mexer para recalibrar: a nota é calculada na hora a partir
/// dos números guardados em <see cref="LigaEstatisticaJogador"/>, então todo o histórico muda junto.
/// Os valores de partida pensam em CPU x CPU, que tem números baixos (zagueiro com uns 11 passes em 90 min).
/// </summary>
public sealed record PesosDaNotaDaLiga
{
    public decimal Base { get; init; } = 6.0m;
    public decimal Minima { get; init; } = 3.0m;
    public decimal Maxima { get; init; } = 10.0m;

    // Gol vale mais para quem não vive de fazer gol.
    public decimal GolAtacante { get; init; } = 1.0m;
    public decimal GolMeia { get; init; } = 1.2m;
    public decimal GolDefensorOuGoleiro { get; init; } = 1.5m;

    /// <summary>Assistência pela sigla da posição do PES: vale mais quanto mais atrás o jogador.</summary>
    public IReadOnlyDictionary<string, decimal> AssistenciaPorPosicao { get; init; } = new Dictionary<string, decimal>
    {
        ["CA"] = 0.7m, ["SA"] = 0.7m, ["PTE"] = 0.7m, ["PTD"] = 0.7m, ["PE"] = 0.7m, ["PD"] = 0.7m,
        ["MLE"] = 0.7m, ["MLD"] = 0.7m, ["MAT"] = 0.7m,
        ["MLG"] = 0.9m,
        ["VOL"] = 1.0m,
        ["LE"] = 1.1m, ["LD"] = 1.1m,
        ["ZC"] = 1.3m, ["GO"] = 1.3m,
    };

    /// <summary>Posição sem peso próprio (ou sem posição): vale o do grupo. A decomposição avisa.</summary>
    public IReadOnlyDictionary<string, decimal> AssistenciaPorGrupo { get; init; } = new Dictionary<string, decimal>
    {
        [NotaDaLiga.Ataque] = 0.7m, [NotaDaLiga.Meio] = 0.7m, [NotaDaLiga.Defesa] = 1.1m, [NotaDaLiga.Goleiro] = 1.3m,
    };

    /// <summary>Nem posição nem grupo conhecidos.</summary>
    public decimal AssistenciaSemPosicao { get; init; } = 0.7m;

    public decimal ChuteAGol { get; init; } = 0.20m;
    public decimal ChuteParaFora { get; init; } = -0.05m;
    public decimal PasseCerto { get; init; } = 0.03m;
    public decimal PasseErrado { get; init; } = -0.08m;
    public decimal DesarmeCerto { get; init; } = 0.20m;
    public decimal DesarmeErrado { get; init; } = -0.05m;
    public decimal Interceptacao { get; init; } = 0.15m;

    /// <summary>Só defensor.</summary>
    public decimal Afastamento { get; init; } = 0.10m;

    /// <summary>Só goleiro.</summary>
    public decimal DefesaDoGoleiro { get; init; } = 0.30m;

    // Gols do adversário enquanto o jogador estava em campo (pelos minutos dos eventos).
    public decimal GolSofridoGoleiro { get; init; } = -0.40m;
    public decimal GolSofridoDefensor { get; init; } = -0.20m;
    public decimal GolSofridoDemais { get; init; } = -0.10m;

    /// <summary>Sem sofrer gol com ele em campo, tendo jogado pelo menos <see cref="MinutosParaSemSofrerGol"/> minutos.</summary>
    public decimal SemSofrerGolGoleiro { get; init; } = 0.5m;
    public decimal SemSofrerGolDefensor { get; init; } = 0.3m;
    public int MinutosParaSemSofrerGol { get; init; } = 60;

    public decimal Falta { get; init; } = -0.10m;
    public decimal Impedimento { get; init; } = -0.10m;

    // Dos eventos da partida.
    public decimal CartaoAmarelo { get; init; } = -0.3m;
    public decimal CartaoVermelho { get; init; } = -2.0m;

    // Pelo placar final.
    public decimal Vitoria { get; init; } = 0.2m;
    public decimal Derrota { get; init; } = -0.2m;

    // Guardados, mas sem peso por enquanto.
    public decimal DistanciaDeDriblePorMetro { get; init; } = 0m;
    public decimal VelocidadeMediaPorKmH { get; init; } = 0m;
}

/// <summary>Um evento da partida, só com o que a nota usa.</summary>
/// <param name="TimeId">Time do evento; no gol contra, o time que ganha o gol (como no cadastro de eventos).</param>
public sealed record EventoParaNota(TipoEvento Tipo, Guid TimeId, int JogadorId, int? JogadorSaiuId, int? Minuto);

/// <summary>
/// Nota própria da liga: base + parcelas tiradas dos números do PES, dos eventos e do placar. Todo jogador
/// tem nota, inclusive quem entrou no fim. A decomposição (<see cref="ResultadoNotaDaLiga.Parcelas"/>) diz
/// de onde veio cada décimo.
/// </summary>
public static class NotaDaLiga
{
    /// <summary>Os pesos em uso no site.</summary>
    public static PesosDaNotaDaLiga Pesos { get; } = new();

    public const string Goleiro = "goleiro";
    public const string Defesa = "defesa";
    public const string Meio = "meio";
    public const string Ataque = "ataque";

    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// O que a nota precisa além dos números do jogador. <paramref name="GolsSofridosEmCampo"/>: gols do
    /// adversário com ele em campo; nulo = não deu para saber pelos eventos, vale o placar final e
    /// <paramref name="PorQueOPlacar"/> diz o motivo (aparece na decomposição).
    /// </summary>
    public sealed record Contexto(
        int GolsPro, int GolsSofridos, int Amarelos = 0, int Vermelhos = 0,
        int? GolsSofridosEmCampo = null, string? PorQueOPlacar = null);

    /// <summary>
    /// Monta o contexto do jogador pelos eventos da partida: cartões dele e os gols sofridos enquanto ele
    /// estava em campo (entrada e saída pelas substituições; o vermelho também tira de campo).
    /// Gol no mesmo minuto da troca conta para quem saiu, não para quem entrou.
    /// </summary>
    public static Contexto ContextoDaPartida(
        LigaEstatisticaJogador j, int golsPro, int golsSofridos, IReadOnlyCollection<EventoParaNota> eventos)
    {
        var id = j.JogadorId;
        var meus = id is not null ? eventos.Where(e => e.TimeId == j.TimeId) : Enumerable.Empty<EventoParaNota>();
        var amarelos = meus.Count(e => e.Tipo == TipoEvento.CartaoAmarelo && e.JogadorId == id);
        var vermelhos = meus.Count(e => e.Tipo == TipoEvento.CartaoVermelho && e.JogadorId == id);

        Contexto PeloPlacar(string motivo) => new(golsPro, golsSofridos, amarelos, vermelhos, null, motivo);

        if (id is null) return PeloPlacar("jogador sem ligação no cadastro");

        // Gols do adversário: gol dele ou gol contra a favor dele. Gol sem autor identificado não vira evento.
        var golsContra = eventos
            .Where(e => e.TimeId != j.TimeId && e.Tipo is TipoEvento.Gol or TipoEvento.GolContra)
            .ToList();
        if (golsContra.Count != golsSofridos) return PeloPlacar("gol sofrido sem registro nos eventos");
        if (golsContra.Any(e => e.Minuto is null)) return PeloPlacar("gol sofrido sem minuto");

        var entrada = meus.FirstOrDefault(e => e.Tipo == TipoEvento.Substituicao && e.JogadorId == id);
        var saida = meus.FirstOrDefault(e => e.Tipo == TipoEvento.Substituicao && e.JogadorSaiuId == id);
        var expulsao = meus.FirstOrDefault(e => e.Tipo == TipoEvento.CartaoVermelho && e.JogadorId == id);

        if (entrada is { Minuto: null }) return PeloPlacar("substituição sem minuto");
        if (saida is { Minuto: null }) return PeloPlacar("substituição sem minuto");
        if (expulsao is { Minuto: null }) return PeloPlacar("expulsão sem minuto");
        // Jogou menos que o jogo todo sem troca nem expulsão nos eventos: não dá para saber quando.
        if (entrada is null && saida is null && expulsao is null && j.Minutos is int min && min < 90)
            return PeloPlacar("entrada ou saída fora dos eventos");

        var inicio = entrada?.Minuto ?? 0;
        var fim = new[] { saida?.Minuto, expulsao?.Minuto }.Where(m => m is not null).Select(m => m!.Value)
            .DefaultIfEmpty(int.MaxValue).Min();
        var emCampo = golsContra.Count(g => g.Minuto > inicio && g.Minuto <= fim);
        return new Contexto(golsPro, golsSofridos, amarelos, vermelhos, emCampo);
    }

    public static ResultadoNotaDaLiga Calcular(LigaEstatisticaJogador j, Contexto c, PesosDaNotaDaLiga? pesos = null)
    {
        var p = pesos ?? Pesos;
        var grupo = GrupoDe(j.Grupo, j.Posicao);

        var parcelas = new List<ParcelaNotaDaLiga>();
        void Somar(int? quantidade, decimal peso, string singular, string plural, string? obs = null)
        {
            if (quantidade is not int q || q <= 0 || peso == 0) return;
            var texto = $"{q} {(q == 1 ? singular : plural)}";
            parcelas.Add(new ParcelaNotaDaLiga(obs is null ? texto : $"{texto} ({obs})", q * peso));
        }
        static int? Diferenca(int? total, int? certos) =>
            total is int t && certos is int ce ? Math.Max(0, t - ce) : null;

        var pesoGol = grupo switch
        {
            Ataque => p.GolAtacante,
            Meio => p.GolMeia,
            Defesa or Goleiro => p.GolDefensorOuGoleiro,
            _ => p.GolAtacante
        };
        Somar(j.Gols, pesoGol, "gol", "gols");

        var (pesoAssistencia, obsAssistencia) = PesoDaAssistencia(j.Posicao, grupo, p);
        Somar(j.Assistencias, pesoAssistencia, "assistência", "assistências", obsAssistencia);

        Somar(j.ChutesAGol, p.ChuteAGol, "chute a gol", "chutes a gol");
        Somar(Diferenca(j.Chutes, j.ChutesAGol), p.ChuteParaFora, "chute para fora", "chutes para fora");
        Somar(j.PassesCertos, p.PasseCerto, "passe certo", "passes certos");
        Somar(Diferenca(j.Passes, j.PassesCertos), p.PasseErrado, "passe errado", "passes errados");
        Somar(j.DesarmesCertos, p.DesarmeCerto, "desarme certo", "desarmes certos");
        Somar(Diferenca(j.Desarmes, j.DesarmesCertos), p.DesarmeErrado, "desarme errado", "desarmes errados");
        Somar(j.Interceptacoes, p.Interceptacao, "interceptação", "interceptações");
        if (grupo == Defesa)
            Somar(j.Afastamentos, p.Afastamento, "afastamento", "afastamentos");
        if (grupo == Goleiro)
            Somar(j.Defesas, p.DefesaDoGoleiro, "defesa", "defesas");

        // Gols sofridos com ele em campo; sem os minutos, o placar final (e a decomposição diz por quê).
        var sofridos = c.GolsSofridosEmCampo ?? c.GolsSofridos;
        var obsSofridos = c.GolsSofridosEmCampo is null && c.PorQueOPlacar is not null
            ? $"pelo placar final: {c.PorQueOPlacar}"
            : null;
        var pesoSofrido = grupo switch
        {
            Goleiro => p.GolSofridoGoleiro,
            Defesa => p.GolSofridoDefensor,
            _ => p.GolSofridoDemais
        };
        Somar(sofridos, pesoSofrido, "gol sofrido em campo", "gols sofridos em campo", obsSofridos);
        if (grupo is Goleiro or Defesa && sofridos == 0 && j.Minutos is int jogou && jogou >= p.MinutosParaSemSofrerGol)
        {
            var bonus = grupo == Goleiro ? p.SemSofrerGolGoleiro : p.SemSofrerGolDefensor;
            if (bonus != 0)
                parcelas.Add(new ParcelaNotaDaLiga(obsSofridos is null ? "sem sofrer gol" : $"sem sofrer gol ({obsSofridos})", bonus));
        }

        Somar(j.Faltas, p.Falta, "falta", "faltas");
        Somar(j.Impedimentos, p.Impedimento, "impedimento", "impedimentos");
        Somar(c.Amarelos, p.CartaoAmarelo, "cartão amarelo", "cartões amarelos");
        Somar(c.Vermelhos, p.CartaoVermelho, "cartão vermelho", "cartões vermelhos");

        if (j.DistanciaDeDrible is decimal drible && drible > 0 && p.DistanciaDeDriblePorMetro != 0)
            parcelas.Add(new ParcelaNotaDaLiga($"{drible.ToString("0.#", Br)} m de drible", drible * p.DistanciaDeDriblePorMetro));
        if (j.VelocidadeMedia is decimal vel && vel > 0 && p.VelocidadeMediaPorKmH != 0)
            parcelas.Add(new ParcelaNotaDaLiga($"{vel.ToString("0.#", Br)} km/h de média", vel * p.VelocidadeMediaPorKmH));

        if (c.GolsPro > c.GolsSofridos && p.Vitoria != 0) parcelas.Add(new ParcelaNotaDaLiga("vitória", p.Vitoria));
        else if (c.GolsPro < c.GolsSofridos && p.Derrota != 0) parcelas.Add(new ParcelaNotaDaLiga("derrota", p.Derrota));

        var bruta = p.Base + parcelas.Sum(x => x.Valor);
        var nota = Math.Round(Math.Clamp(bruta, p.Minima, p.Maxima), 1, MidpointRounding.AwayFromZero);
        return new ResultadoNotaDaLiga(nota, parcelas);
    }

    /// <summary>Peso da assistência pela posição; sem peso próprio, o do grupo, com a observação para a decomposição.</summary>
    private static (decimal Peso, string? Obs) PesoDaAssistencia(string? posicao, string? grupo, PesosDaNotaDaLiga p)
    {
        var sigla = posicao?.Trim().ToUpperInvariant();
        if (sigla is not null && p.AssistenciaPorPosicao.TryGetValue(sigla, out var peso)) return (peso, null);

        var qual = string.IsNullOrEmpty(sigla) ? "sem posição" : $"posição {sigla} sem peso próprio";
        return grupo is not null && p.AssistenciaPorGrupo.TryGetValue(grupo, out var doGrupo)
            ? (doGrupo, $"{qual}: valor do grupo {grupo}")
            : (p.AssistenciaSemPosicao, $"{qual}: valor padrão");
    }

    /// <summary>Grupo do JSON; sem ele, deduzido da sigla da posição do PES.</summary>
    public static string? GrupoDe(string? grupo, string? posicao)
    {
        var g = grupo?.Trim().ToLowerInvariant();
        if (g is Goleiro or Defesa or Meio or Ataque) return g;
        return posicao?.Trim().ToUpperInvariant() switch
        {
            "GO" => Goleiro,
            "ZC" or "ZAG" or "LE" or "LD" => Defesa,
            "VOL" or "MLG" or "MLE" or "MLD" or "MAT" or "MC" or "ME" or "MD" => Meio,
            "SA" or "CA" or "PE" or "PD" or "PTE" or "PTD" => Ataque,
            _ => null
        };
    }

    /// <summary>"+1,0" / "−0,25": o sinal sempre aparece, com duas casas só quando precisa.</summary>
    public static string TextoParcela(decimal valor)
    {
        var abs = Math.Abs(valor).ToString(Math.Round(valor, 1) == valor ? "0.0" : "0.00", Br);
        return (valor < 0 ? "−" : "+") + abs;
    }
}

public sealed record ParcelaNotaDaLiga(string Descricao, decimal Valor);

public sealed record ResultadoNotaDaLiga(decimal Nota, IReadOnlyList<ParcelaNotaDaLiga> Parcelas);
