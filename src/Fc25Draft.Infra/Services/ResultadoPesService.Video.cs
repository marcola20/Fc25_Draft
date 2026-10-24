using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Vídeo do jogo no YouTube, mandado pelo painel do Auto_PES21 depois de subir a estreia. Acha a
/// partida do mesmo jeito que o envio do resultado, então vale antes ou depois dele (e com o
/// resultado segurado).
/// </summary>
public partial class ResultadoPesService
{
    public async Task<VideoPesRespostaDto> DefinirVideoAsync(VideoPesRequest request, Guid? partidaAnterior, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Casa?.Time) || string.IsNullOrWhiteSpace(request.Fora?.Time))
            throw new ResultadoPesException(ResultadoPesErro.Invalido, "Faltam os nomes dos times.");
        var videoId = YoutubeLink.ExtrairId(request.Link)
            ?? throw new ResultadoPesException(ResultadoPesErro.Invalido, $"Não reconheci esse link do YouTube: \"{request.Link}\".");

        var busca = new ResultadoPesRequest(request.Video, request.Serie, request.Rodada, request.Casa, request.Fora,
            Eventos: null, Notas: null, Status: "ok", GravadoEm: request.GravadoEm);
        var alvo = await AcharPartidaAsync(busca, partidaAnterior, null, ct);

        // Jogo de mata-mata sem partida: ela só nasce com o resultado.
        if (alvo.PartidaId is not Guid partidaId)
            throw new ResultadoPesException(ResultadoPesErro.NaoEncontrado,
                $"{request.Casa!.Time} x {request.Fora!.Time} é da {alvo.Onde} ({alvo.Liga}) e a partida ainda não existe: "
                + "ela é criada quando o resultado é importado. Atribua o vídeo depois disso.");

        var partida = await _db.LigaPartidas.AsNoTracking()
            .Where(p => p.PartidaId == partidaId)
            .Select(p => new { p.YoutubeVideoId, Casa = p.TimeCasa.TeamName, Fora = p.TimeFora.TeamName })
            .FirstAsync(ct);

        await _ligaAdmin.DefinirVideoPartidaAsync(partidaId, videoId, ct);

        var anterior = partida.YoutubeVideoId is { } v && v != videoId ? v : null;
        return new VideoPesRespostaDto(partidaId, alvo.Liga, alvo.Onde, partida.Casa, partida.Fora, videoId, anterior);
    }
}
