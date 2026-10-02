namespace Fc25Draft.Core.Interfaces;

public record FotoJogadorArquivo(byte[] Imagem, string ContentType, DateTime AtualizadaEm);

/// <summary>Uma foto do PES enviada pelo script de importação (imagem em base64).</summary>
public record FotoPesRequest(int PesId, string ImagemBase64);

// Versao muda a cada troca (vai no endereço da imagem, para o navegador não mostrar a do cache).
public record FotoJogadorInfo(bool TemFoto, string? Origem, string Versao);

/// <summary>Fotos dos jogadores: importadas do PES ou trocadas à mão pelo admin.</summary>
public interface IFotosJogadoresService
{
    Task<FotoJogadorArquivo?> ObterAsync(int playerId, CancellationToken ct);

    Task<FotoJogadorInfo> InfoAsync(int playerId, CancellationToken ct);

    /// <summary>IDs do PES dos jogadores ligados ao PES que não têm foto trocada à mão (o que o script deve mandar).</summary>
    Task<IReadOnlyList<int>> PesIdsParaImportarAsync(CancellationToken ct);

    /// <summary>Grava as fotos do PES dos jogadores ligados; nunca mexe em foto trocada à mão. Retorna quantas gravou.</summary>
    Task<int> ImportarDoPesAsync(IReadOnlyList<FotoPesRequest> fotos, CancellationToken ct);

    /// <summary>Troca a foto à mão (admin). Ela passa a valer acima da do PES.</summary>
    Task TrocarAsync(int playerId, byte[] imagem, CancellationToken ct);

    /// <summary>Tira a foto; a próxima importação do PES volta a pôr a do jogo, se houver.</summary>
    Task RemoverAsync(int playerId, CancellationToken ct);
}
