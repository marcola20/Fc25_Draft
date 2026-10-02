using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Fotos dos jogadores. As do PES chegam pelo script de importação (por ID do PES, só para quem está ligado
/// ao PES); a trocada à mão pelo admin fica acima e a importação nunca a sobrescreve.
/// </summary>
public class FotosJogadoresService : IFotosJogadoresService
{
    private const int TamanhoMaximo = 300 * 1024;
    private const int MaximoPorEnvio = 200;

    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public FotosJogadoresService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    private DateTime Agora => _time.GetUtcNow().UtcDateTime;

    public async Task<FotoJogadorArquivo?> ObterAsync(int playerId, CancellationToken ct) =>
        await _db.FotosJogadores.AsNoTracking()
            .Where(f => f.PlayerId == playerId)
            .Select(f => new FotoJogadorArquivo(f.Imagem, f.ContentType, f.AtualizadaEm))
            .FirstOrDefaultAsync(ct);

    public async Task<FotoJogadorInfo> InfoAsync(int playerId, CancellationToken ct)
    {
        var foto = await _db.FotosJogadores.AsNoTracking()
            .Where(f => f.PlayerId == playerId)
            .Select(f => new { f.Origem, f.AtualizadaEm })
            .FirstOrDefaultAsync(ct);
        return foto is null
            ? new FotoJogadorInfo(false, null, "0")
            : new FotoJogadorInfo(true, foto.Origem, foto.AtualizadaEm.Ticks.ToString("x"));
    }

    public async Task<IReadOnlyList<int>> PesIdsParaImportarAsync(CancellationToken ct) =>
        await _db.PlayerAtributos.AsNoTracking()
            .Where(a => a.PesId != null
                        && !_db.FotosJogadores.Any(f => f.PlayerId == a.PlayerId && f.Origem == FotoJogador.OrigemManual))
            .Select(a => a.PesId!.Value)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(ct);

    public async Task<int> ImportarDoPesAsync(IReadOnlyList<FotoPesRequest> fotos, CancellationToken ct)
    {
        if (fotos.Count > MaximoPorEnvio)
            throw new InvalidOperationException($"Mande no máximo {MaximoPorEnvio} fotos por vez.");

        var pesIds = fotos.Select(f => f.PesId).Distinct().ToList();
        var jogadores = await _db.PlayerAtributos.AsNoTracking()
            .Where(a => a.PesId != null && pesIds.Contains(a.PesId.Value))
            .Select(a => new { a.PlayerId, PesId = a.PesId!.Value })
            .ToListAsync(ct);
        var ids = jogadores.Select(j => j.PlayerId).ToList();
        var existentes = await _db.FotosJogadores.Where(f => ids.Contains(f.PlayerId)).ToDictionaryAsync(f => f.PlayerId, ct);

        var gravadas = 0;
        foreach (var foto in fotos)
        {
            if (!TentarLer(foto.ImagemBase64, out var imagem, out var tipo)) continue;

            foreach (var jogador in jogadores.Where(j => j.PesId == foto.PesId))
            {
                if (existentes.TryGetValue(jogador.PlayerId, out var atual))
                {
                    if (atual.Origem == FotoJogador.OrigemManual) continue;
                    if (atual.Imagem.AsSpan().SequenceEqual(imagem)) continue;
                    atual.Imagem = imagem;
                    atual.ContentType = tipo;
                    atual.AtualizadaEm = Agora;
                }
                else
                {
                    var nova = new FotoJogador
                    {
                        PlayerId = jogador.PlayerId,
                        Imagem = imagem,
                        ContentType = tipo,
                        Origem = FotoJogador.OrigemPes,
                        AtualizadaEm = Agora
                    };
                    _db.FotosJogadores.Add(nova);
                    existentes[jogador.PlayerId] = nova;
                }
                gravadas++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return gravadas;
    }

    public async Task TrocarAsync(int playerId, byte[] imagem, CancellationToken ct)
    {
        if (imagem.Length == 0 || imagem.Length > TamanhoMaximo)
            throw new InvalidOperationException("A imagem precisa ter até 300 KB.");
        var tipo = Tipo(imagem) ?? throw new InvalidOperationException("Use uma imagem WebP, PNG ou JPEG.");
        if (!await _db.Players.AnyAsync(p => p.PlayerId == playerId, ct))
            throw new InvalidOperationException("Jogador não encontrado.");

        var atual = await _db.FotosJogadores.FirstOrDefaultAsync(f => f.PlayerId == playerId, ct);
        if (atual is null)
        {
            atual = new FotoJogador { PlayerId = playerId };
            _db.FotosJogadores.Add(atual);
        }

        atual.Imagem = imagem;
        atual.ContentType = tipo;
        atual.Origem = FotoJogador.OrigemManual;
        atual.AtualizadaEm = Agora;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(int playerId, CancellationToken ct) =>
        await _db.FotosJogadores.Where(f => f.PlayerId == playerId).ExecuteDeleteAsync(ct);

    private static bool TentarLer(string base64, out byte[] imagem, out string tipo)
    {
        imagem = Array.Empty<byte>();
        tipo = string.Empty;
        try
        {
            imagem = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return false;
        }

        if (imagem.Length == 0 || imagem.Length > TamanhoMaximo) return false;
        tipo = Tipo(imagem) ?? string.Empty;
        return tipo.Length > 0;
    }

    // Pelo começo do arquivo, não pelo que o cliente diz.
    private static string? Tipo(byte[] b) => b switch
    {
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => "image/webp",
        [0x89, (byte)'P', (byte)'N', (byte)'G', ..] => "image/png",
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        _ => null
    };
}
