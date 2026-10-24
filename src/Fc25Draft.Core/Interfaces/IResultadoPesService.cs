using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Importa o resultado de uma partida simulada no PES 2021 (JSON do Auto_PES21).</summary>
public interface IResultadoPesService
{
    /// <summary>
    /// Acha a partida pelo mandante x visitante, grava placar e eventos (substituindo os que
    /// havia) e recalcula a classificação. Com <paramref name="simular"/> faz tudo e desfaz.
    /// <paramref name="partidaAnterior"/> = partida que recebeu o envio anterior deste JSON; desfaz
    /// a dúvida quando o par se repete em outra competição. Recusa com
    /// <see cref="Exceptions.ResultadoPesException"/> sem gravar nada.
    /// </summary>
    Task<ResultadoPesRespostaDto> ImportarAsync(ResultadoPesRequest request, string jsonBruto, Guid? partidaAnterior, bool simular, CancellationToken ct);

    /// <summary>
    /// Entrada do envio do Auto_PES21. Com a chave "segurar" ligada, valida (simulando) e guarda o
    /// JSON em <c>ResultadosPesPendentes</c> sem tocar na partida; reenvio do mesmo vídeo ou do mesmo
    /// jogo substitui o pendente. Jogo já importado (correção do que já saiu) e chave desligada
    /// vão direto para <see cref="ImportarAsync"/>.
    /// </summary>
    Task<ResultadoPesRespostaDto> ReceberAsync(ResultadoPesRequest request, string jsonBruto, Guid? partidaAnterior, bool simular, CancellationToken ct);

    /// <summary>
    /// Grava o vídeo do YouTube na partida achada como no envio do resultado (mandante x visitante e
    /// o título do vídeo). Recusa com <see cref="Exceptions.ResultadoPesException"/> sem gravar nada.
    /// </summary>
    Task<VideoPesRespostaDto> DefinirVideoAsync(VideoPesRequest request, Guid? partidaAnterior, CancellationToken ct);

    Task<bool> SegurandoAsync(CancellationToken ct);
    Task DefinirSegurarAsync(bool segurar, CancellationToken ct);

    /// <summary>Resultados segurados, na ordem de liberação (data do jogo, depois envio).</summary>
    Task<IReadOnlyList<ResultadoPesPendenteDto>> PendentesAsync(CancellationToken ct);

    /// <summary>
    /// Importa de verdade o resultado segurado (encerra a partida e mostra para todos) e tira da fila.
    /// Se falhar, grava o erro no pendente e lança <see cref="Exceptions.ResultadoPesException"/>.
    /// </summary>
    Task<ResultadoPesRespostaDto> LiberarAsync(Guid pendenteId, CancellationToken ct);

    /// <summary>Valida de novo (ex.: o mata-mata ganhou os times depois de liberar a fase anterior).</summary>
    Task<ResultadoPesPendenteDto> RevalidarAsync(Guid pendenteId, CancellationToken ct);

    Task DescartarAsync(Guid pendenteId, CancellationToken ct);

    /// <summary>
    /// Tira as notas dos JSONs já importados e grava por jogador. Com <paramref name="somenteSemNotas"/>,
    /// só as partidas que ainda não têm nenhuma nota gravada. Retorna quantas notas foram gravadas.
    /// </summary>
    Task<int> ReprocessarNotasAsync(bool somenteSemNotas, CancellationToken ct);
}
