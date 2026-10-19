using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs;

public record DestaqueLiderDto(Guid LigaId, string LigaNome, Guid TimeId, string TimeNome, int Pontos, int Jogos);

public record DestaqueFaseDto(Guid TimeId, string TimeNome, string Sequencia, string Tipo);

/// <summary>Quem lidera uma contagem da temporada (gols, assistências): Total é o número dele.</summary>
public record DestaqueJogadorDto(int JogadorId, string Nome, string TimeNome, int Total);

public record DestaqueNotaDto(int JogadorId, string Nome, string TimeNome, decimal Media, int Jogos);

public record DestaqueTransferenciaDto(int JogadorId, string JogadorNome, string? DeTime, string? ParaTime, decimal Valor, DateTime Data);

/// <summary>Resumo do momento para a página inicial. Cada item é nulo/vazio quando não há o que mostrar.</summary>
public record DestaquesDto(
    IReadOnlyList<DestaqueLiderDto> Lideres,
    DestaqueFaseDto? MelhorFase,
    IReadOnlyList<DestaqueJogadorDto> Artilheiros,
    IReadOnlyList<DestaqueJogadorDto> Garcons,
    DestaqueNotaDto? MelhorNota,
    DestaqueTransferenciaDto? MaiorTransferencia);
