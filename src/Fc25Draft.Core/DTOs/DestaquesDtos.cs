using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs;

public record DestaqueLiderDto(Guid LigaId, string LigaNome, Guid TimeId, string TimeNome, int Pontos, int Jogos);

/// <summary>Sequência que está valendo: Jogos sem perder (melhor fase) ou sem vencer (pior), com o retrospecto nela.</summary>
public record DestaqueFaseDto(Guid TimeId, string TimeNome, int Jogos, int Vitorias, int Empates, int Derrotas);

/// <summary>Quem lidera uma contagem da temporada (gols, assistências, clean sheets): Total é o número dele.</summary>
public record DestaqueJogadorDto(int JogadorId, string Nome, string TimeNome, int Total);

public record DestaqueNotaDto(int JogadorId, string Nome, string TimeNome, decimal Media, int Jogos);

/// <summary>Resumo do momento para a página inicial. Cada item é nulo/vazio quando não há o que mostrar.</summary>
public record DestaquesDto(
    IReadOnlyList<DestaqueLiderDto> Lideres,
    IReadOnlyList<DestaqueFaseDto> MelhorFase,
    IReadOnlyList<DestaqueFaseDto> PiorFase,
    IReadOnlyList<DestaqueJogadorDto> Artilheiros,
    IReadOnlyList<DestaqueJogadorDto> Garcons,
    DestaqueNotaDto? MelhorNota,
    IReadOnlyList<DestaqueJogadorDto> Paredoes);
