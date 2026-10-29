using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs;

/// <summary>Gols que valeram pontos: o da vitória (deixou o time na frente de vez num jogo ganho por um gol) e o do empate.</summary>
public record GolDecisivoJogadorDto(
    int JogadorId, string JogadorNome, Guid TimeId, string TimeNome, int DaVitoria, int DoEmpate,
    IReadOnlyList<GolDecisivoJogoDto> Jogos)
{
    public int Total => DaVitoria + DoEmpate;
}

/// <summary>Um gol decisivo: o jogo (placar final), o minuto e o placar que o gol deixou (PlacarCasa x PlacarFora).</summary>
public record GolDecisivoJogoDto(
    Guid PartidaId, string Rotulo,
    string CasaNome, string ForaNome, int GolsCasa, int GolsFora,
    int Minuto, int PlacarCasa, int PlacarFora, bool DaVitoria);

/// <summary>
/// Pontos buscados depois de sair atrás (virada = 3, empate buscado = 1) e pontos que escaparam depois de estar
/// na frente (empatou = 2, perdeu = 3).
/// </summary>
public record ViradaTimeDto(
    Guid TimeId, string TimeNome,
    int Viradas, int EmpatesBuscados, int PontosBuscados,
    int VantagensPerdidas, int PontosPerdidos,
    IReadOnlyList<ViradaTimeJogoDto> Jogos)
{
    public int Saldo => PontosBuscados - PontosPerdidos;
}

public enum TipoViradaJogo { Virada, EmpateBuscado, VantagemPerdida }

/// <summary>
/// Um jogo do time na tabela de viradas. Gols = por quanto ele chegou a estar atrás (virada, empate buscado) ou na
/// frente (vantagem perdida); Pontos = +3, +1, −2 ou −3.
/// </summary>
public record ViradaTimeJogoDto(
    Guid PartidaId, string Rotulo,
    string CasaNome, string ForaNome, int GolsCasa, int GolsFora,
    TipoViradaJogo Tipo, int Gols, int Pontos);

/// <summary>Jogo virado; MaiorDesvantagem = por quantos gols o vencedor chegou a estar perdendo.</summary>
public record ViradaJogoDto(
    Guid PartidaId, string Rotulo,
    Guid CasaId, string CasaNome, Guid ForaId, string ForaNome, int GolsCasa, int GolsFora,
    Guid VencedorId, int MaiorDesvantagem);

/// <summary>Gols feitos e sofridos em cada período de 15 minutos (o último inclui os acréscimos).</summary>
public record GolsPorPeriodoTimeDto(Guid TimeId, string TimeNome, IReadOnlyList<int> Feitos, IReadOnlyList<int> Sofridos);

/// <summary>
/// O que sai do minuto dos gols numa competição. Só entram os jogos com todos os gols registrados com minuto e
/// batendo com o placar (JogosAnalisados de JogosDaCompeticao); W.O. fica de fora.
/// </summary>
public record MomentosDosGolsDto(
    int JogosAnalisados,
    int JogosDaCompeticao,
    IReadOnlyList<GolDecisivoJogadorDto> Decisivos,
    IReadOnlyList<ViradaTimeDto> Times,
    IReadOnlyList<ViradaJogoDto> Viradas,
    IReadOnlyList<string> Periodos,
    IReadOnlyList<GolsPorPeriodoTimeDto> PorPeriodo);
