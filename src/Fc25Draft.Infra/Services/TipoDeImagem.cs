namespace Fc25Draft.Infra.Services;

/// <summary>Tipo da imagem pelo começo do arquivo, não pelo que o cliente diz (fotos de jogador e de treinador).</summary>
internal static class TipoDeImagem
{
    /// <summary>image/webp, image/png ou image/jpeg; nulo para qualquer outra coisa.</summary>
    public static string? Detectar(byte[] b) => b switch
    {
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => "image/webp",
        [0x89, (byte)'P', (byte)'N', (byte)'G', ..] => "image/png",
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        _ => null
    };
}
