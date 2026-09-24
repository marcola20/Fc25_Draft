using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs;

public record DestaqueLiderDto(Guid LigaId, string LigaNome, Guid TimeId, string TimeNome, int Pontos, int Jogos);

public record DestaqueFaseDto(Guid TimeId, string TimeNome, string Sequencia, string Tipo);

public record DestaqueArtilheiroDto(int JogadorId, string Nome, string TimeNome, int Gols);

public record DestaqueTransferenciaDto(int JogadorId, string JogadorNome, string? DeTime, string? ParaTime, decimal Valor, DateTime Data);

/// <summary>Resumo do momento para a página inicial. Cada item é nulo/vazio quando não há o que mostrar.</summary>
public record DestaquesDto(
    IReadOnlyList<DestaqueLiderDto> Lideres,
    DestaqueFaseDto? MelhorFase,
    IReadOnlyList<DestaqueArtilheiroDto> Artilheiros,
    DestaqueTransferenciaDto? MaiorTransferencia);
