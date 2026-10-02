namespace Fc25Draft.Core.Entities;

/// <summary>
/// Foto do jogador, guardada no banco (no Render os arquivos somem a cada deploy). Vem do PES pelo script
/// de importação ou é trocada à mão pelo admin; a trocada à mão nunca é sobrescrita pela importação.
/// </summary>
public class FotoJogador
{
    public const string OrigemPes = "PES";
    public const string OrigemManual = "MANUAL";

    public int PlayerId { get; set; }
    public byte[] Imagem { get; set; } = null!;

    /// <summary>Tipo da imagem (image/webp, image/png, image/jpeg).</summary>
    public string ContentType { get; set; } = null!;

    /// <summary><see cref="OrigemPes"/> ou <see cref="OrigemManual"/>.</summary>
    public string Origem { get; set; } = null!;

    public DateTime AtualizadaEm { get; set; }

    public Player Player { get; set; } = null!;
}
