using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

public static class DiretoriaLabels
{
    public static string Faixa(FaixaConfianca faixa) => faixa switch
    {
        FaixaConfianca.Prestigiado => "Prestigiado",
        FaixaConfianca.Estavel => "Estável",
        FaixaConfianca.SobObservacao => "Sob observação",
        FaixaConfianca.Pressionado => "Pressionado",
        _ => "Cadeira balançando"
    };

    public static string Emoji(FaixaConfianca faixa) => faixa switch
    {
        FaixaConfianca.Prestigiado => "💚",
        FaixaConfianca.Estavel => "🙂",
        FaixaConfianca.SobObservacao => "👀",
        FaixaConfianca.Pressionado => "⚠️",
        _ => "🔥"
    };

    /// <summary>Classe de badge do Bootstrap para a faixa.</summary>
    public static string Badge(FaixaConfianca faixa) => faixa switch
    {
        FaixaConfianca.Prestigiado => "bg-success",
        FaixaConfianca.Estavel => "bg-primary",
        FaixaConfianca.SobObservacao => "bg-secondary",
        FaixaConfianca.Pressionado => "bg-warning text-dark",
        _ => "bg-danger"
    };

    /// <summary>Cor da barra do termômetro (classe de fundo do Bootstrap).</summary>
    public static string Barra(FaixaConfianca faixa) => faixa switch
    {
        FaixaConfianca.Prestigiado => "bg-success",
        FaixaConfianca.Estavel => "bg-primary",
        FaixaConfianca.SobObservacao => "bg-secondary",
        FaixaConfianca.Pressionado => "bg-warning",
        _ => "bg-danger"
    };

    public static string Situacao(SituacaoMeta situacao) => situacao switch
    {
        SituacaoMeta.Cumprida => "Meta cumprida",
        SituacaoMeta.Superada => "Meta superada",
        SituacaoMeta.Fracassada => "Meta não cumprida",
        SituacaoMeta.SemMeta => "Sem meta",
        _ => "Em andamento"
    };

    public static string SituacaoBadge(SituacaoMeta situacao) => situacao switch
    {
        SituacaoMeta.Cumprida => "bg-success",
        SituacaoMeta.Superada => "bg-warning text-dark",
        SituacaoMeta.Fracassada => "bg-danger",
        SituacaoMeta.SemMeta => "bg-secondary",
        _ => "bg-light text-dark border"
    };

    public static string SituacaoEmoji(SituacaoMeta situacao) => situacao switch
    {
        SituacaoMeta.Cumprida => "✅",
        SituacaoMeta.Superada => "🌟",
        SituacaoMeta.Fracassada => "❌",
        _ => ""
    };

    /// <summary>Meta da liga em palavras: "Brigar pelo título (top 3)", "Subir (até o 2º)".</summary>
    public static string MetaLiga(Divisao divisao, int metaPosicao, int limiteSerieA)
    {
        if (divisao == Divisao.SerieB)
            return metaPosicao <= 1 ? "Subir como campeão" : $"Subir (terminar até o {metaPosicao}º)";

        return metaPosicao switch
        {
            1 => "Ser campeão",
            <= DiretoriaCriterios.MetaFavoritos => $"Brigar pelo título (top {metaPosicao})",
            <= DiretoriaCriterios.MetaParteDeCima => $"Parte de cima (top {metaPosicao})",
            _ when metaPosicao == limiteSerieA => $"Escapar do playoff (até o {metaPosicao}º)",
            _ => $"Terminar até o {metaPosicao}º"
        };
    }

    public static string MetaCopa(FasePremiacao? meta) => meta switch
    {
        FasePremiacao.Campeao => "Ser campeão",
        FasePremiacao.Vice => "Chegar à final",
        FasePremiacao.Semifinal => "Chegar à semifinal",
        FasePremiacao.Quartas => "Chegar às quartas",
        _ => "Sem meta (passar de fase é lucro)"
    };

    /// <summary>Onde o time está na Copa agora.</summary>
    public static string FaseCopa(FasePremiacao fase, bool aindaVivo) => (fase, aindaVivo) switch
    {
        (FasePremiacao.Campeao, _) => "Campeão 🏆",
        (FasePremiacao.Vice, true) => "Na final",
        (FasePremiacao.Vice, false) => "Vice-campeão",
        (FasePremiacao.Semifinal, true) => "Na semifinal",
        (FasePremiacao.Semifinal, false) => "Caiu na semifinal",
        (FasePremiacao.Quartas, true) => "Nas quartas",
        (FasePremiacao.Quartas, false) => "Caiu nas quartas",
        (_, true) => "Fase de grupos",
        _ => "Caiu na fase de grupos"
    };
}
