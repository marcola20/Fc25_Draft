using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Pesos e limites da Diretoria. Ficam aqui para a página mostrar exatamente os mesmos
/// critérios usados no cálculo.
/// </summary>
public static class DiretoriaCriterios
{
    // Expectativa de cada time na liga (nota de 0 a 100). Somam 100.
    public const int PesoHistorico = 70;
    public const int PesoElenco = 30;

    /// <summary>Quantas temporadas anteriores entram no histórico.</summary>
    public const int TemporadasNoHistorico = 3;

    /// <summary>A temporada anterior vale este tanto; as outras valem 1.</summary>
    public const int PesoTemporadaAnterior = 2;

    /// <summary>Força do elenco = média de overall dos 11 melhores.</summary>
    public const int JogadoresNoXI = 11;

    // Série A, pela posição esperada: favoritos brigam pelo título, os seguintes pela parte de cima
    // e o resto precisa escapar do playoff (o limite sai das zonas da liga).
    public const int FavoritosAte = 3;
    public const int MetaFavoritos = 3;
    public const int ParteDeCimaAte = 6;
    public const int MetaParteDeCima = 5;

    /// <summary>Série A: terminar este tanto acima do limite da meta (ou ser campeão) é superar.</summary>
    public const int PosicoesParaSuperar = 2;

    // Copa, pelo pote do sorteio. Potes seguintes ficam sem meta: passar de fase é lucro.
    public const FasePremiacao MetaPote1 = FasePremiacao.Semifinal;
    public const FasePremiacao MetaPote2 = FasePremiacao.Quartas;

    // Confiança da diretoria (0 a 100).
    public const double ConfiancaInicial = 65;
    public const double K = 12;

    public const double LimitePrestigiado = 80;
    public const double LimiteEstavel = 50;
    public const double LimiteSobObservacao = 35;
    public const double LimitePressionado = 20;

    // Ultimato: ao cair para "cadeira balançando", a diretoria exige pontos num prazo de jogos.
    public const int UltimatoPontos = 4;
    public const int UltimatoJogos = 3;

    /// <summary>Recusado o pedido de demissão, a confiança vai para este valor (voto de confiança).</summary>
    public const double ConfiancaVotoDeConfianca = 35;

    /// <summary>Depois do voto de confiança, quantos jogos sem ultimato novo.</summary>
    public const int JogosDeCarencia = 3;
}

public enum FaixaConfianca
{
    CadeiraBalancando = 0,
    Pressionado = 1,
    SobObservacao = 2,
    Estavel = 3,
    Prestigiado = 4
}

public enum SituacaoMeta
{
    EmAndamento = 0,
    Cumprida,
    Superada,
    Fracassada,
    /// <summary>Copa, potes de baixo: não havia meta e o time não passou de fase.</summary>
    SemMeta
}

/// <summary>Posição final de um time numa liga de uma temporada anterior.</summary>
/// <param name="TimesNaSerieA">Tamanho da Série A naquela temporada: na conta, o 1º da B vem logo depois do último da A.</param>
public record DiretoriaHistoricoInput(Guid TimeId, int Temporada, Divisao Divisao, int Posicao, int TimesNaSerieA);

/// <summary>Meta calculada para um time numa liga.</summary>
/// <param name="MediaHistorico">Posição média no histórico (contando a B depois da A); nulo sem histórico.</param>
public record MetaLigaCalculada(
    Guid TimeId, int PosicaoEsperada, double Nota, int MetaPosicao, double? MediaHistorico, double ForcaXI);

/// <summary>
/// Jogo encerrado da temporada, com a chance de vitória do mandante pelo Elo de antes do jogo.
/// Pênaltis contam como empate (vale o placar do tempo normal), igual ao resto do site.
/// </summary>
public record DiretoriaJogoInput(
    Guid PartidaId, string Competicao, DateTime Data,
    Guid CasaId, Guid ForaId, int GolsCasa, int GolsFora, double EsperadoCasa);

/// <summary>Como a confiança de um time ficou depois de um jogo (ou de um <see cref="Evento"/> da diretoria).</summary>
/// <param name="Evento">Nulo num jogo; num ajuste da diretoria, o que aconteceu (sem partida nem adversário).</param>
public record PontoConfianca(
    Guid PartidaId, string Competicao, DateTime Data, Guid AdversarioId,
    int GolsPro, int GolsContra, double Variacao, double Valor, EventoDiretoria? Evento = null);

public enum EventoDiretoria
{
    /// <summary>Pedido de demissão recusado: a confiança vai para o voto de confiança.</summary>
    VotoDeConfianca = 1,
    /// <summary>Pedido aceito: o técnico novo começa com a confiança inicial.</summary>
    NovoTecnico = 2
}

/// <summary>Ajuste da confiança de um time num momento (decisão sobre um pedido de demissão).</summary>
public record DiretoriaAjusteInput(Guid TimeId, DateTime Data, EventoDiretoria Evento);

public enum SituacaoUltimato
{
    EmAndamento = 0,
    Cumprido,
    Fracassado
}

/// <summary>Ultimato da diretoria: começa no jogo que levou à cadeira balançando e vale para os jogos seguintes.</summary>
/// <param name="PartidaFinalId">Jogo que cumpriu ou fez fracassar o ultimato; nulo em andamento.</param>
public record UltimatoCalculado(
    Guid TimeId, Guid PartidaOrigemId, DateTime Inicio, int Pontos, int Jogos,
    SituacaoUltimato Situacao, Guid? PartidaFinalId, DateTime? Fim)
{
    public int JogosRestantes => DiretoriaCriterios.UltimatoJogos - Jogos;
}

public static class Diretoria
{
    // ── Metas ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Metas de uma liga: cada time ganha uma nota (histórico + elenco), a nota vira posição esperada
    /// e a posição esperada vira meta. Na Série B todo mundo tem a mesma meta: subir.
    /// </summary>
    public static IReadOnlyList<MetaLigaCalculada> MetasDaLiga(
        int temporada,
        Divisao divisao,
        LigaRegraZonas regra,
        IReadOnlyDictionary<Guid, double> forcaXIPorTime,
        IEnumerable<DiretoriaHistoricoInput> historico)
    {
        var times = forcaXIPorTime.Keys.ToList();
        if (times.Count == 0) return Array.Empty<MetaLigaCalculada>();

        var medias = MediasHistorico(temporada, times, historico);

        // Sem histórico nenhum o time conta como o pior do grupo nessa parte da nota.
        var comHistorico = medias.Where(m => m.Value is not null).ToDictionary(m => m.Key, m => m.Value!.Value);
        var notaHistorico = Escala(comHistorico, menorEhMelhor: true);
        var notaElenco = Escala(forcaXIPorTime, menorEhMelhor: false);

        var ordenados = times
            .Select(id => (
                TimeId: id,
                Nota: (notaHistorico.GetValueOrDefault(id, 0) * DiretoriaCriterios.PesoHistorico
                       + notaElenco[id] * DiretoriaCriterios.PesoElenco) / 100.0))
            .OrderByDescending(x => x.Nota)
            .ThenByDescending(x => forcaXIPorTime[x.TimeId])
            .ToList();

        var total = times.Count;
        return ordenados
            .Select((x, i) => new MetaLigaCalculada(
                x.TimeId, i + 1, Math.Round(x.Nota, 1),
                MetaPelaPosicaoEsperada(divisao, regra, i + 1, total),
                medias[x.TimeId] is double m ? Math.Round(m, 2) : null,
                Math.Round(forcaXIPorTime[x.TimeId], 2)))
            .ToArray();
    }

    /// <summary>Série B: subir (zona de acesso, direto ou playoff). Série A: faixa pela posição esperada.</summary>
    public static int MetaPelaPosicaoEsperada(Divisao divisao, LigaRegraZonas regra, int posicaoEsperada, int totalTimes)
    {
        if (divisao == Divisao.SerieB)
            return Math.Max(1, regra.VagasDiretas + regra.VagasPlayoff);

        var limite = LimiteSerieA(regra, totalTimes);
        if (posicaoEsperada <= DiretoriaCriterios.FavoritosAte) return Math.Min(DiretoriaCriterios.MetaFavoritos, limite);
        if (posicaoEsperada <= DiretoriaCriterios.ParteDeCimaAte) return Math.Min(DiretoriaCriterios.MetaParteDeCima, limite);
        return limite;
    }

    /// <summary>Série A: última posição fora do playoff e da queda.</summary>
    public static int LimiteSerieA(LigaRegraZonas regra, int totalTimes)
    {
        var limite = totalTimes - regra.VagasDiretas - regra.VagasPlayoff;
        return limite >= 1 ? limite : totalTimes;
    }

    public static FasePremiacao? MetaDaCopa(int pote) => pote switch
    {
        1 => DiretoriaCriterios.MetaPote1,
        2 => DiretoriaCriterios.MetaPote2,
        _ => null
    };

    /// <summary>
    /// Posição média nas últimas temporadas antes de <paramref name="temporada"/>, a anterior com peso maior.
    /// A Série B entra depois da A (1º da B = último da A + 1).
    /// </summary>
    private static Dictionary<Guid, double?> MediasHistorico(int temporada, IReadOnlyList<Guid> times, IEnumerable<DiretoriaHistoricoInput> historico)
    {
        var anteriores = historico.Where(h => h.Temporada < temporada).ToList();
        var temporadas = anteriores.Select(h => h.Temporada).Distinct()
            .OrderByDescending(t => t)
            .Take(DiretoriaCriterios.TemporadasNoHistorico)
            .ToList();
        var maisRecente = temporadas.Count > 0 ? temporadas[0] : (int?)null;

        var medias = new Dictionary<Guid, double?>();
        foreach (var id in times)
        {
            double soma = 0, pesos = 0;
            foreach (var h in anteriores.Where(h => h.TimeId == id && temporadas.Contains(h.Temporada)))
            {
                var peso = h.Temporada == maisRecente ? DiretoriaCriterios.PesoTemporadaAnterior : 1;
                var geral = h.Divisao == Divisao.SerieB ? h.TimesNaSerieA + h.Posicao : h.Posicao;
                soma += geral * peso;
                pesos += peso;
            }
            medias[id] = pesos > 0 ? soma / pesos : null;
        }

        return medias;
    }

    /// <summary>O melhor do grupo fica com 100 e o pior com 0; todos iguais ficam com 50.</summary>
    private static Dictionary<Guid, double> Escala(IReadOnlyDictionary<Guid, double> valores, bool menorEhMelhor)
    {
        if (valores.Count == 0) return new Dictionary<Guid, double>();

        var min = valores.Values.Min();
        var max = valores.Values.Max();
        if (max - min < 1e-9) return valores.ToDictionary(v => v.Key, _ => 50.0);

        return valores.ToDictionary(
            v => v.Key,
            v => (menorEhMelhor ? max - v.Value : v.Value - min) * 100 / (max - min));
    }

    // ── Avaliação ──────────────────────────────────────────────────────────

    /// <summary>
    /// Série A: dentro da meta cumpre; <see cref="DiretoriaCriterios.PosicoesParaSuperar"/> acima dela, ou campeão, supera.
    /// Série B: subir (direto ou no playoff) supera; ir ao playoff e perder cumpre.
    /// </summary>
    /// <param name="subiuNoPlayoff">Série B, time na zona de playoff: resultado do playoff de acesso (nulo = ainda não jogado).</param>
    public static SituacaoMeta AvaliarLiga(
        Divisao divisao, LigaRegraZonas regra, int metaPosicao, int posicao, bool encerrada, bool? subiuNoPlayoff)
    {
        if (!encerrada) return SituacaoMeta.EmAndamento;

        if (divisao == Divisao.SerieB)
        {
            if (posicao <= regra.VagasDiretas || posicao == 1) return SituacaoMeta.Superada;
            if (posicao > metaPosicao) return SituacaoMeta.Fracassada;
            return subiuNoPlayoff switch
            {
                true => SituacaoMeta.Superada,
                false => SituacaoMeta.Cumprida,
                null => SituacaoMeta.EmAndamento
            };
        }

        if (posicao == 1 || posicao <= metaPosicao - DiretoriaCriterios.PosicoesParaSuperar) return SituacaoMeta.Superada;
        return posicao <= metaPosicao ? SituacaoMeta.Cumprida : SituacaoMeta.Fracassada;
    }

    /// <summary>
    /// Copa: chegar à fase da meta cumpre (já garantido, mesmo com o time vivo); ir além supera.
    /// Sem meta, chegar às quartas supera.
    /// </summary>
    /// <param name="alcancada">Fase mais longe a que o time chegou (quem ainda vai jogar uma fase já chegou nela).</param>
    public static SituacaoMeta AvaliarCopa(FasePremiacao? meta, FasePremiacao alcancada, bool aindaVivo)
    {
        var alvo = meta ?? FasePremiacao.Quartas;
        var superar = meta is null ? alvo : (FasePremiacao)((int)alvo - 1);

        if (alcancada <= superar) return SituacaoMeta.Superada;
        if (meta is not null && alcancada <= alvo) return SituacaoMeta.Cumprida;
        if (aindaVivo) return SituacaoMeta.EmAndamento;
        return meta is null ? SituacaoMeta.SemMeta : SituacaoMeta.Fracassada;
    }

    // ── Confiança ──────────────────────────────────────────────────────────

    /// <summary>
    /// Confiança de cada time depois de cada jogo: começa em <see cref="DiretoriaCriterios.ConfiancaInicial"/>
    /// e anda K × (resultado − chance esperada) × peso da goleada, entre 0 e 100.
    /// Os jogos precisam vir na ordem em que foram disputados.
    /// </summary>
    public static Dictionary<Guid, List<PontoConfianca>> Confianca(
        IEnumerable<DiretoriaJogoInput> jogosEmOrdem, IEnumerable<DiretoriaAjusteInput>? ajustes = null)
    {
        var pontos = new Dictionary<Guid, List<PontoConfianca>>();
        double Atual(Guid id) =>
            pontos.TryGetValue(id, out var lista) && lista.Count > 0 ? lista[^1].Valor : DiretoriaCriterios.ConfiancaInicial;

        void Registrar(DiretoriaJogoInput j, Guid timeId, Guid adversario, int pro, int contra, double variacao)
        {
            var antes = Atual(timeId);
            var depois = Math.Clamp(antes + variacao, 0, 100);
            if (!pontos.TryGetValue(timeId, out var lista)) pontos[timeId] = lista = new List<PontoConfianca>();
            lista.Add(new PontoConfianca(j.PartidaId, j.Competicao, j.Data, adversario, pro, contra,
                Math.Round(depois - antes, 1), Math.Round(depois, 1)));
        }

        // Ajustes entram na linha do tempo depois dos jogos que já tinham acontecido na hora da decisão.
        var pendentes = new Queue<DiretoriaAjusteInput>((ajustes ?? Array.Empty<DiretoriaAjusteInput>()).OrderBy(a => a.Data));

        void AplicarAjustesAte(DateTime? data)
        {
            while (pendentes.Count > 0 && (data is null || pendentes.Peek().Data <= data))
            {
                var a = pendentes.Dequeue();
                var antes = Atual(a.TimeId);
                var depois = a.Evento == EventoDiretoria.VotoDeConfianca
                    ? DiretoriaCriterios.ConfiancaVotoDeConfianca
                    : DiretoriaCriterios.ConfiancaInicial;
                if (!pontos.TryGetValue(a.TimeId, out var lista)) pontos[a.TimeId] = lista = new List<PontoConfianca>();
                lista.Add(new PontoConfianca(Guid.Empty, NomeDoEvento(a.Evento), a.Data, Guid.Empty, 0, 0,
                    Math.Round(depois - antes, 1), depois, a.Evento));
            }
        }

        foreach (var j in jogosEmOrdem)
        {
            AplicarAjustesAte(j.Data);
            var resultado = j.GolsCasa > j.GolsFora ? 1.0 : j.GolsCasa < j.GolsFora ? 0.0 : 0.5;
            var variacao = DiretoriaCriterios.K
                           * PowerRankingCriterios.MultiplicadorMargem(Math.Abs(j.GolsCasa - j.GolsFora))
                           * (resultado - j.EsperadoCasa);

            Registrar(j, j.CasaId, j.ForaId, j.GolsCasa, j.GolsFora, variacao);
            Registrar(j, j.ForaId, j.CasaId, j.GolsFora, j.GolsCasa, -variacao);
        }

        AplicarAjustesAte(null);
        return pontos;
    }

    public static string NomeDoEvento(EventoDiretoria evento) => evento switch
    {
        EventoDiretoria.VotoDeConfianca => "Voto de confiança",
        _ => "Técnico novo"
    };

    /// <summary>
    /// Ultimatos de um time ao longo da temporada. Ao cair para "cadeira balançando", a diretoria exige
    /// <see cref="DiretoriaCriterios.UltimatoPontos"/> pontos nos <see cref="DiretoriaCriterios.UltimatoJogos"/>
    /// jogos seguintes. Cumpriu: segue a vida. Ficou impossível: fracassou e a diretoria pede a demissão; até
    /// a organização decidir não há ultimato novo. Voto de confiança dá alguns jogos de carência; técnico novo
    /// começa do zero.
    /// </summary>
    public static IReadOnlyList<UltimatoCalculado> Ultimatos(Guid timeId, IReadOnlyList<PontoConfianca> pontos)
    {
        var ultimatos = new List<UltimatoCalculado>();
        UltimatoCalculado? atual = null;
        var aguardandoDecisao = false;
        var carencia = 0;

        foreach (var p in pontos)
        {
            if (p.Evento is EventoDiretoria evento)
            {
                atual = null;
                aguardandoDecisao = false;
                carencia = evento == EventoDiretoria.VotoDeConfianca ? DiretoriaCriterios.JogosDeCarencia : 0;
                continue;
            }

            if (atual is not null)
            {
                var pontosDoJogo = p.GolsPro > p.GolsContra ? 3 : p.GolsPro == p.GolsContra ? 1 : 0;
                atual = atual with { Pontos = atual.Pontos + pontosDoJogo, Jogos = atual.Jogos + 1 };

                if (atual.Pontos >= DiretoriaCriterios.UltimatoPontos)
                {
                    ultimatos.Add(atual with { Situacao = SituacaoUltimato.Cumprido, PartidaFinalId = p.PartidaId, Fim = p.Data });
                    atual = null;
                }
                else if (atual.Pontos + 3 * atual.JogosRestantes < DiretoriaCriterios.UltimatoPontos)
                {
                    ultimatos.Add(atual with { Situacao = SituacaoUltimato.Fracassado, PartidaFinalId = p.PartidaId, Fim = p.Data });
                    atual = null;
                    aguardandoDecisao = true;
                }
                continue;
            }

            if (aguardandoDecisao) continue;
            if (carencia > 0)
            {
                carencia--;
                continue;
            }

            if (Faixa(p.Valor) == FaixaConfianca.CadeiraBalancando)
                atual = new UltimatoCalculado(timeId, p.PartidaId, p.Data, 0, 0, SituacaoUltimato.EmAndamento, null, null);
        }

        if (atual is not null) ultimatos.Add(atual);
        return ultimatos;
    }

    /// <summary>Quantos times aparecem em "na corda bamba" no resumo da rodada.</summary>
    public const int NaCordaNoResumo = 3;

    /// <summary>
    /// A diretoria numa rodada: quem mudou de faixa nos jogos dela e os times mais perto da demissão
    /// (abaixo de "estável") logo depois dela.
    /// </summary>
    public static DiretoriaResumoRodadaDto ResumoDaRodada(IEnumerable<DiretoriaTimeDto> times, IReadOnlyCollection<Guid> partidasDaRodada)
    {
        var lista = times.ToList();
        var mudancas = new List<DiretoriaMudancaDto>();
        DateTime? fimDaRodada = null;

        foreach (var time in lista)
        {
            for (int i = 0; i < time.Historico.Count; i++)
            {
                var ponto = time.Historico[i];
                if (!partidasDaRodada.Contains(ponto.PartidaId)) continue;

                if (fimDaRodada is null || ponto.Data > fimDaRodada) fimDaRodada = ponto.Data;

                var antes = Faixa(i > 0 ? time.Historico[i - 1].Valor : DiretoriaCriterios.ConfiancaInicial);
                var depois = Faixa(ponto.Valor);
                if (antes != depois)
                    mudancas.Add(new DiretoriaMudancaDto(time.TimeNome, antes, depois, ponto.Valor));
            }
        }

        // Ninguém jogou: nada a dizer sobre a diretoria nesta rodada.
        if (fimDaRodada is not DateTime fim)
            return new DiretoriaResumoRodadaDto(Array.Empty<DiretoriaMudancaDto>(), Array.Empty<DiretoriaNaCordaDto>());

        var naCorda = lista
            .Select(t => (t.TimeNome, Valor: t.Historico.LastOrDefault(p => p.Data <= fim)?.Valor ?? DiretoriaCriterios.ConfiancaInicial))
            .Where(x => x.Valor < DiretoriaCriterios.LimiteEstavel)
            .OrderBy(x => x.Valor)
            .Take(NaCordaNoResumo)
            .Select(x => new DiretoriaNaCordaDto(x.TimeNome, Faixa(x.Valor), x.Valor))
            .ToArray();

        return new DiretoriaResumoRodadaDto(
            mudancas.OrderBy(m => m.Depois > m.Antes).ThenBy(m => m.Confianca).ToArray(),
            naCorda);
    }

    public static FaixaConfianca Faixa(double confianca) => confianca switch
    {
        >= DiretoriaCriterios.LimitePrestigiado => FaixaConfianca.Prestigiado,
        >= DiretoriaCriterios.LimiteEstavel => FaixaConfianca.Estavel,
        >= DiretoriaCriterios.LimiteSobObservacao => FaixaConfianca.SobObservacao,
        >= DiretoriaCriterios.LimitePressionado => FaixaConfianca.Pressionado,
        _ => FaixaConfianca.CadeiraBalancando
    };

    /// <summary>
    /// Notícias do Plantão quando a confiança muda de faixa depois de um jogo. Só a mudança vira
    /// notícia: ficar na mesma faixa não gera nada.
    /// </summary>
    public static IReadOnlyList<PlantaoNoticiaDto> NoticiasDeConfianca(
        IReadOnlyDictionary<Guid, List<PontoConfianca>> pontos, IReadOnlyDictionary<Guid, string> nomes)
    {
        var noticias = new List<PlantaoNoticiaDto>();

        foreach (var (timeId, lista) in pontos)
        {
            var nome = nomes.GetValueOrDefault(timeId, "?");
            var antes = Faixa(DiretoriaCriterios.ConfiancaInicial);

            foreach (var p in lista)
            {
                var agora = Faixa(p.Valor);
                // Ajuste da diretoria (voto de confiança, técnico novo) tem notícia própria.
                if (p.Evento is not null)
                {
                    antes = agora;
                    continue;
                }
                if (agora != antes)
                {
                    var adversario = nomes.GetValueOrDefault(p.AdversarioId, "?");
                    var (emoji, manchete) = Manchete(nome, antes, agora);
                    noticias.Add(new PlantaoNoticiaDto(
                        p.Data.AddSeconds(3), PlantaoCategoria.Diretoria, emoji, manchete,
                        $"Depois do {p.GolsPro} x {p.GolsContra} contra o {adversario} · confiança {p.Valor:0} de 100",
                        $"/teams/details/{timeId}", nome));
                }
                antes = agora;
            }
        }

        return noticias;
    }

    private static (string Emoji, string Manchete) Manchete(string time, FaixaConfianca antes, FaixaConfianca agora)
    {
        var subiu = agora > antes;
        return agora switch
        {
            FaixaConfianca.Prestigiado => ("💚", $"Lua de mel: diretoria do {time} está em êxtase com o técnico"),
            FaixaConfianca.Estavel when subiu => ("😮‍💨", $"Diretoria do {time} volta a confiar no trabalho"),
            FaixaConfianca.Estavel => ("🙂", $"Diretoria do {time} esfria a empolgação, mas segue confiante"),
            FaixaConfianca.SobObservacao when subiu => ("📈", $"{time} reage e alivia a pressão da diretoria"),
            FaixaConfianca.SobObservacao => ("👀", $"Diretoria do {time} começa a desconfiar"),
            FaixaConfianca.Pressionado when subiu => ("🩹", $"Vitória dá sobrevida ao técnico do {time}"),
            FaixaConfianca.Pressionado => ("⚠️", $"Diretoria do {time} perde a paciência"),
            _ => ("🔥", $"Cadeira balança no {time}: diretoria em crise")
        };
    }
}
