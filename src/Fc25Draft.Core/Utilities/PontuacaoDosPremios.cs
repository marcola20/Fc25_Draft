using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>O que um jogador fez na temporada, para a sugestão dos prêmios.</summary>
/// <param name="Jogos">Jogos com nota oficial.</param>
/// <param name="Media">Média da nota oficial (a da liga quando a temporada tem registros).</param>
/// <param name="Gols">Gols e assistências oficiais (dos eventos).</param>
/// <param name="SemSofrer">Jogos com nota em que o time não sofreu gol.</param>
/// <param name="JogosComRegistro">Jogos com o registro individual do PES; os números abaixo somam só esses.</param>
public sealed record TemporadaDoJogador(
    PositionType Posicao, int Jogos, decimal Media, int Gols, int Assistencias, int SemSofrer,
    int JogosComRegistro = 0, int DesarmesCertos = 0, int Interceptacoes = 0, int Afastamentos = 0,
    int PassesCertos = 0, int ChutesAGol = 0, int Defesas = 0, int JogosNoGol = 0);

/// <summary>
/// Pontuação da sugestão dos prêmios: média da nota oficial + a contribuição da POSIÇÃO do jogador, por jogo.
/// Zagueiro e lateral somam desarmes, interceptações, afastamentos e jogos sem sofrer gol (gol e assistência
/// contam, com peso menor); goleiro soma defesas e jogos sem sofrer gol; meia soma gol, assistência e o jogo de
/// construção e marcação; atacante soma gol, assistência e chutes a gol. Vale para MVP, ROY, DPOY e GKOY (DPOY e
/// GKOY só olham a posição deles). Os pesos ficam aqui, num lugar só. Temporada sem registro individual: só os
/// termos de gol, assistência e jogos sem sofrer gol contam.
/// </summary>
public static class PontuacaoDosPremios
{
    // Defensor (zagueiro e laterais), por jogo com registro.
    public const decimal DefensorDesarmeCerto = 0.20m;
    public const decimal DefensorInterceptacao = 0.15m;
    public const decimal DefensorAfastamento = 0.10m;
    // Por jogo com nota.
    public const decimal DefensorSemSofrerGol = 0.5m;   // vezes a taxa de jogos sem sofrer gol
    public const decimal DefensorGol = 0.3m;
    public const decimal DefensorAssistencia = 0.2m;

    // Goleiro.
    public const decimal GoleiroDefesa = 0.30m;          // por jogo no gol com registro
    public const decimal GoleiroSemSofrerGol = 1.0m;     // vezes a taxa de jogos sem sofrer gol

    // Meia (volante, meias), por jogo.
    public const decimal MeiaGol = 0.5m;
    public const decimal MeiaAssistencia = 0.5m;
    public const decimal MeiaPasseCerto = 0.01m;
    public const decimal MeiaDesarmeCerto = 0.10m;
    public const decimal MeiaInterceptacao = 0.08m;

    // Atacante (pontas e atacantes), por jogo.
    public const decimal AtacanteGol = 0.6m;
    public const decimal AtacanteAssistencia = 0.4m;
    public const decimal AtacanteChuteAGol = 0.05m;

    public enum Setor { Goleiro, Defesa, Meio, Ataque }

    public static Setor SetorDe(PositionType p) => p switch
    {
        PositionType.Goleiro => Setor.Goleiro,
        PositionType.Zagueiro or PositionType.LateralEsquerdo or PositionType.LateralDireito => Setor.Defesa,
        PositionType.Volante or PositionType.MeiaLigacao or PositionType.MeiaAtacante
            or PositionType.MeiaEsquerda or PositionType.MeiaDireita => Setor.Meio,
        _ => Setor.Ataque
    };

    /// <summary>Média + contribuição da posição.</summary>
    public static decimal Pontos(TemporadaDoJogador t) => Math.Round(t.Media + Contribuicao(t), 3);

    /// <summary>O que a posição soma por jogo, além da nota.</summary>
    public static decimal Contribuicao(TemporadaDoJogador t)
    {
        decimal PorJogo(decimal total) => t.Jogos > 0 ? total / t.Jogos : 0;
        decimal PorRegistro(decimal total) => t.JogosComRegistro > 0 ? total / t.JogosComRegistro : 0;
        var semSofrer = t.Jogos > 0 ? (decimal)t.SemSofrer / t.Jogos : 0;

        return SetorDe(t.Posicao) switch
        {
            Setor.Goleiro =>
                (t.JogosNoGol > 0 ? t.Defesas * GoleiroDefesa / t.JogosNoGol : 0)
                + semSofrer * GoleiroSemSofrerGol,
            Setor.Defesa =>
                PorRegistro(t.DesarmesCertos * DefensorDesarmeCerto + t.Interceptacoes * DefensorInterceptacao
                            + t.Afastamentos * DefensorAfastamento)
                + semSofrer * DefensorSemSofrerGol
                + PorJogo(t.Gols * DefensorGol + t.Assistencias * DefensorAssistencia),
            Setor.Meio =>
                PorJogo(t.Gols * MeiaGol + t.Assistencias * MeiaAssistencia)
                + PorRegistro(t.PassesCertos * MeiaPasseCerto + t.DesarmesCertos * MeiaDesarmeCerto
                              + t.Interceptacoes * MeiaInterceptacao),
            _ =>
                PorJogo(t.Gols * AtacanteGol + t.Assistencias * AtacanteAssistencia)
                + PorRegistro(t.ChutesAGol * AtacanteChuteAGol)
        };
    }
}
