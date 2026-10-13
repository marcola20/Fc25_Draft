using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.DTOs;

/// <summary>Um jogo da rodada, com o palpite de quem está olhando (se já palpitou).</summary>
public record BolaoJogoDto(
    Guid PartidaId,
    Guid TimeCasaId,
    string TimeCasa,
    Guid TimeForaId,
    string TimeFora,
    bool Encerrado,
    int? GolsCasa,
    int? GolsFora,
    int? PalpiteCasa,
    int? PalpiteFora,
    int? Pontos,
    bool ComExtras = false,
    int? JogadorCasaId = null,
    string? JogadorCasa = null,
    int? JogadorForaId = null,
    string? JogadorFora = null,
    int? PontosPlacar = null,
    int? ExtraCasa = null,
    int? ExtraFora = null);

/// <summary>Uma rodada aberta ou fechada para palpite.</summary>
public record BolaoRodadaDto(
    Guid RodadaId,
    Guid LigaId,
    string Competicao,
    TipoCompetition Tipo,
    int Numero,
    string Titulo,
    DateTime? Quando,
    bool Aberta,
    IReadOnlyList<BolaoJogoDto> Jogos)
{
    public int Palpitados => Jogos.Count(j => j.PalpiteCasa is not null);
    public bool Completa => Jogos.Count > 0 && Palpitados == Jogos.Count;
    public int Pontos => Jogos.Sum(j => j.Pontos ?? 0);
}

/// <summary>O palpite de alguém num jogo, para a tela de "o que o pessoal chutou".</summary>
public record BolaoPalpiteDeAlguemDto(
    Guid TreinadorId,
    string Nome,
    string? TimeNome,
    int GolsCasa,
    int GolsFora,
    int? Pontos,
    string? JogadorCasa = null,
    string? JogadorFora = null);

/// <summary>Um jogador que dá para escolher na aposta G/A.</summary>
public record BolaoJogadorOpcaoDto(int JogadorId, string Nome, string Posicao);

/// <summary>O palpite de alguém num jogo da rodada, para o adm conferir antes da bola rolar.</summary>
public record BolaoPalpiteNoJogoDto(
    Guid PartidaId,
    int GolsCasa,
    int GolsFora,
    string? JogadorCasa,
    string? JogadorFora,
    int? Pontos);

public record BolaoPalpitesDeAlguemDto(
    Guid TreinadorId,
    string Nome,
    string? TimeNome,
    IReadOnlyList<BolaoPalpiteNoJogoDto> Palpites);

/// <summary>Tudo o que foi chutado numa rodada, e quem ainda não chutou nada.</summary>
public record BolaoPalpitesDaRodadaDto(
    IReadOnlyList<BolaoPalpitesDeAlguemDto> Pessoas,
    IReadOnlyList<string> Faltam);

/// <summary>Uma linha do ranking do bolão.</summary>
public record BolaoRankingLinhaDto(
    int Posicao,
    Guid TreinadorId,
    string Nome,
    string? TimeNome,
    int Pontos,
    int Palpites,
    int Cravadas,
    int Acertos)
{
    /// <summary>Média de pontos por palpite — compara quem entrou depois com quem joga desde o começo.</summary>
    public decimal Media => Palpites == 0 ? 0m : Math.Round((decimal)Pontos / Palpites, 1);
}

public record BolaoPalpiteRequest(Guid PartidaId, int GolsCasa, int GolsFora, int? JogadorCasaId = null, int? JogadorForaId = null);

/// <summary>O que a pessoa fez no bolão, para a página de carreira.</summary>
public record BolaoResumoDoTreinadorDto(
    int Pontos,
    int Palpites,
    int Cravadas,
    int? PosicaoGeral,
    int RodadasVencidas);
