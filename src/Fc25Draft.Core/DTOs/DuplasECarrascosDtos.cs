using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs;

/// <summary>Cabeçalho de um jogo nas listas de duplas e carrascos.</summary>
public record JogoResumoDto(Guid PartidaId, string Competicao, string Rotulo, string CasaNome, string ForaNome, int GolsCasa, int GolsFora);

/// <summary>
/// Dois jogadores que combinaram em gol (um deu a assistência, o outro marcou), nos dois sentidos.
/// A é quem mais serviu o outro; TimeNome é o time do gol mais recente da dupla.
/// </summary>
public record DuplaDto(
    int JogadorAId, string JogadorANome, int JogadorBId, string JogadorBNome,
    Guid TimeId, string TimeNome, int AParaB, int BParaA,
    IReadOnlyList<DuplaGolDto> Gols)
{
    public int Total => AParaB + BParaA;
}

/// <summary>Um gol da dupla; DeAParaB = A deu a assistência e B marcou.</summary>
public record DuplaGolDto(JogoResumoDto Jogo, int? Minuto, bool DeAParaB);

/// <summary>Os gols de um jogador contra um mesmo adversário, em todos os jogos em que marcou contra ele.</summary>
public record CarrascoDto(
    int JogadorId, string JogadorNome, Guid AdversarioId, string AdversarioNome, int Gols,
    IReadOnlyList<CarrascoJogoDto> Jogos);

/// <summary>Um jogo em que o carrasco marcou: por qual time ele jogava, quantos gols fez e em que minutos.</summary>
public record CarrascoJogoDto(JogoResumoDto Jogo, string TimeNome, int Gols, IReadOnlyList<int> Minutos);
